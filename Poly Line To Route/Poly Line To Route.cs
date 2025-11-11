namespace PolyLineToRoute
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net.Messages;

    [GQIMetaData(Name = "Poly Line To Route")]
    public sealed class PolyLineToRoute : IGQIDataSource, IGQIOnInit, IGQIInputArguments, IGQIOnPrepareFetch
    {
        private readonly GQIStringArgument _mobilityManagerArgument = new GQIStringArgument("Mobility Manager") { IsRequired = false };
        private readonly GQIStringArgument _tripNameArgument = new GQIStringArgument("Trip Name") { IsRequired = true };

        private GQIDMS _dms;

        private string _mobilityManagerElementName;
        private string _tripName;

        private LiteElementInfoEvent _mobilityManagerElement;

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;

            return default;
        }

        public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
        {
            _mobilityManagerElement = GetMobilityManager();

            if(_mobilityManagerElement == null)
            {
                throw new ArgumentException($"Element '{_mobilityManagerElementName}' not found.");
            }

            return default;
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[] { _mobilityManagerArgument, _tripNameArgument };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            _mobilityManagerElementName = args.GetArgumentValue(_mobilityManagerArgument);
            _tripName = args.GetArgumentValue(_tripNameArgument);

            return default;
        }

        public GQIColumn[] GetColumns()
        {
            return new GQIColumn[]
            {
                new GQIStringColumn("Identifier"),
                new GQIDoubleColumn("Source Latitude"),
                new GQIDoubleColumn("Source Longitude"),
                new GQIDoubleColumn("Destination Latitude"),
                new GQIDoubleColumn("Destination Longitude"),
            };
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            return new GQIPage(GetGeoPoints().ToArray())
            {
                HasNextPage = false,
            };
        }

        private List<GQIRow> GetGeoPoints()
        {
            var rows = new List<GQIRow>();

            if (string.IsNullOrEmpty(_mobilityManagerElementName) || string.IsNullOrEmpty(_tripName))
            {
                return rows;
            }

            if (_mobilityManagerElementName == null)
            {
                return rows;
            }

            if (_tripName == null)
            {
                return rows;
            }

            var polyLine = GetTripPolyLine();
            var geoPoints = GeoParser.Parse(polyLine);
            int i = 1;

            foreach (var geoPoint in geoPoints)
            {
                rows.Add(new GQIRow(
                new[]
                {
                    new GQICell { Value = i++.ToString()},
                    new GQICell { Value = geoPoint.SourceLatitude },
                    new GQICell { Value = geoPoint.SourceLongitude },
                    new GQICell { Value = geoPoint.DestinationLatitude },
                    new GQICell { Value = geoPoint.DestinationLongitude },
                }));
            }

            return rows;
        }

        private LiteElementInfoEvent GetMobilityManager()
        {
            var eInfo = GetLiteElementInfo.ByName(_mobilityManagerElementName);
            LiteElementInfoEvent eData = (LiteElementInfoEvent)_dms.SendMessage(eInfo);

            if (eData == null)
            {
                throw new GenIfException($"No data collector element found for '{_mobilityManagerElementName}'");
            }

            if (eData.State != ElementState.Active)
            {
                throw new GenIfException($"Element '{_mobilityManagerElementName}' is not active");
            }

            if (eData.Protocol != "Google Maps Platform")
            {
                throw new GenIfException($"The data collector element '{eData.Name}' is not using the expected protocol 'Arqiva DAB Data Collector'");
            }

            return eData;
        }

        private string GetTripPolyLine()
        {
            int dmaid = _mobilityManagerElement.DataMinerID;
            int eid = _mobilityManagerElement.ElementID;
            int table = 800;
            string[] filters = new[] { $"value=807=={_tripName}" };

            GetPartialTableMessage req = new GetPartialTableMessage(dmaid, eid, table, filters);
            ParameterChangeEventMessage data = _dms.SendMessage(req) as ParameterChangeEventMessage;

            var polyLine = string.Empty;

            if (data != null && data.NewValue != null && data.NewValue.IsArray && !data.NewValue.IsEmpty)
            {
                polyLine = data.NewValue.GetTableCell(0, 7)?.CellValue.GetAsStringValue();
            }

            return polyLine;
        }
    }

    public class GeoSegment
    {
        public double SourceLatitude { get; set; }

        public double SourceLongitude { get; set; }

        public double DestinationLatitude { get; set; }

        public double DestinationLongitude { get; set; }
    }

    public class GeoParser
    {
        public static List<GeoSegment> Parse(string input)
        {
            var segments = new List<GeoSegment>();

            var pairs = input.Split(new[] { "],[" }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Replace("[", string.Empty).Replace("]", string.Empty))
                             .Select(p => p.Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray())
                             .ToList();

            for (int i = 0; i < pairs.Count - 1; i++)
            {
                segments.Add(new GeoSegment
                {
                    SourceLongitude = pairs[i][0],
                    SourceLatitude = pairs[i][1],
                    DestinationLongitude = pairs[i + 1][0],
                    DestinationLatitude = pairs[i + 1][1],
                });
            }

            return segments;
        }
    }
}
