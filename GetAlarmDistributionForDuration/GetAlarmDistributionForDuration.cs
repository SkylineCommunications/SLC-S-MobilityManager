namespace GetAlarmHistoryForDistance
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Core.DataMinerSystem.Common;
    using Skyline.DataMiner.Net.Filters;
    using Skyline.DataMiner.Net.Messages;

    [GQIMetaData(Name = "Get Alarm History For Duration")]
    public sealed class GetAlarmHistoryForDistance : IGQIDataSource
        , IGQIOnInit
        , IGQIInputArguments
        , IGQIOptimizableDataSource
        , IGQIOnPrepareFetch
        , IGQIUpdateable
        , IGQIOnDestroy
    {
        private sealed class AlarmInterval
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string Severity { get; set; }
            public DateTime TimeOfArrival { get; set; }
            public DateTime RootCreationTime { get; set; }
        }

        private readonly GQIStringArgument _mobilityManagerArgument = new GQIStringArgument("Mobility Manager") { IsRequired = false };
        private GQIDMS _dms;
        private IDms _idms;
        private GQIDateTimeArgument _startPeriod = new GQIDateTimeArgument("Start Period");
        private GQIDateTimeArgument _endPeriod = new GQIDateTimeArgument("End Period");
        private GQIStringArgument _routeName = new GQIStringArgument("Route Name") { IsRequired = true };
        private string _mobilityManagerElementName = "Mobility Manager";

        private DateTime _startTime;
        private DateTime _endTime;
        private string _routeDisplayName;
        private IDmsElement _mobilityManagerElement;

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
            _idms = _dms.GetConnection().GetDms();
            return default;
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[]
            {
                _mobilityManagerArgument,
                _startPeriod,
                _endPeriod,
                _routeName,
            };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            args.TryGetArgumentValue(_startPeriod, out _startTime);
            args.TryGetArgumentValue(_endPeriod, out _endTime);
            args.TryGetArgumentValue(_routeName, out _routeDisplayName);
            _mobilityManagerElementName = args.GetArgumentValue(_mobilityManagerArgument);
            return default;
        }

        public GQIColumn[] GetColumns()
        {
            return new GQIColumn[]
            {
                new GQIStringColumn("Severity"),
                new GQIDateTimeColumn("CreationTime"),
                new GQIDateTimeColumn("EndTime"),
                new GQIDateTimeColumn("TimeOfArrival"),
                new GQIDateTimeColumn("RootCreationTime"),
            };
        }

        public IGQIQueryNode Optimize(IGQIDataSourceNode currentNode, IGQICoreOperator nextOperator)
        {
            return currentNode.Append(nextOperator);
        }

        public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
        {
            _mobilityManagerElement = _idms.GetElement(_mobilityManagerElementName);
            if (_mobilityManagerElement == null)
                throw new ArgumentException("Element '" + _mobilityManagerElementName + "' not found.");
            return default;
        }

        public void OnStartUpdates(IGQIUpdater updater)
        {
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            return new GQIPage(GetAlarmRows().ToArray()) { HasNextPage = false };
        }

        private List<GQIRow> GetAlarmRows()
        {
            var rows = new List<GQIRow>();
            if (string.IsNullOrEmpty(_mobilityManagerElementName) || string.IsNullOrEmpty(_routeDisplayName)) return rows;

            var alarms = GetHistoricalAlarms(_mobilityManagerElement, _startTime, _endTime, _routeDisplayName);
            if (alarms == null || alarms.Count == 0)
                throw new ArgumentException("Alarm list is empty");

            var intervals = BuildIntervals(alarms, _endTime);

            foreach (var seg in intervals)
            {
                rows.Add(new GQIRow(new[]
                {
                    new GQICell { Value = seg.Severity },
                    new GQICell { Value = seg.Start.ToUniversalTime() },
                    new GQICell { Value = seg.End.ToUniversalTime() },
                    new GQICell { Value = seg.TimeOfArrival.ToUniversalTime() },
                    new GQICell { Value = seg.RootCreationTime.ToUniversalTime() },
                }));
            }

            return rows;
        }

        private static List<AlarmInterval> BuildIntervals(List<AlarmEventMessage> alarms, DateTime windowEnd)
        {
            var ordered = alarms
                .OrderBy(a => a.CreationTime)
                .ThenBy(a => a.TimeOfArrival)
                .ToList();

            var intervals = new List<AlarmInterval>();
            if (ordered.Count == 0)
                return intervals;

            for (int i = 0; i < ordered.Count; i++)
            {
                var start = ordered[i].CreationTime;
                var end = (i < ordered.Count - 1) ? ordered[i + 1].CreationTime : windowEnd;
                if (end <= start)
                    continue;

                intervals.Add(new AlarmInterval
                {
                    Start = start,
                    End = end,
                    Severity = ordered[i].Severity,
                    TimeOfArrival = ordered[i].TimeOfArrival,
                    RootCreationTime = ordered[i].RootCreationTime,
                });
            }

            var merged = new List<AlarmInterval>();
            for (int i = 0; i < intervals.Count; i++)
            {
                if (merged.Count > 0)
                {
                    var last = merged[merged.Count - 1];
                    if (last.Severity == intervals[i].Severity && last.End == intervals[i].Start)
                    {
                        last.End = intervals[i].End;
                        continue;
                    }
                }

                merged.Add(intervals[i]);
            }

            return merged;
        }

        private List<AlarmEventMessage> GetHistoricalAlarms(IDmsElement element, DateTime startTime, DateTime endTime, string routeDisplayName)
        {
            var alarmFilterItemelement = new AlarmFilterItemString(AlarmFilterField.ElementID, AlarmFilterCompareType.Equality, new[] { element.DmsElementId.AgentId + "/" + element.DmsElementId.ElementId });
            var alarmFilterItemParam = new AlarmFilterItemParameterID
            {
                CompareType = AlarmFilterCompareType.Equality,
                Parameters = new[] { new ParameterIndexPair(804, routeDisplayName) },
                ProtocolNameVersion = element.Protocol.Name.ToLower() + "/" + element.Protocol.Version.ToLower(),
            };

            var alarmFilter = new AlarmFilter
            {
                FilterItems = new AlarmFilterItem[]
                {
                    alarmFilterItemelement,
                    alarmFilterItemParam,
                },
            };

            var getAlarmDetailsFromDbMessage = new GetAlarmDetailsFromDbMessage
            {
                AlarmTable = true,
                StartTime = startTime,
                EndTime = endTime,
                Filter = alarmFilter,
            };

            var alarmsFromDB = _dms.SendMessages(getAlarmDetailsFromDbMessage).OfType<AlarmEventMessage>().ToList();
            if (alarmsFromDB == null || alarmsFromDB.Count == 0)
                return new List<AlarmEventMessage>();

            return alarmsFromDB.OrderBy(a => a.CreationTime).ThenBy(a => a.TimeOfArrival).ToList();
        }

        public void OnStopUpdates()
        {
        }

        public OnDestroyOutputArgs OnDestroy(OnDestroyInputArgs args)
        {
            return default;
        }
    }
}
