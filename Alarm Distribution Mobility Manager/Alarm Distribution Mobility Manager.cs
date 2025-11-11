using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

//---------------------------------
// Distribution.cs
//---------------------------------

[GQIMetaData(Name = "Tarik > Alarm report > Distribution")]
public sealed class Distribution : IGQIDataSource, IGQIOnInit, IGQIInputArguments
{
    private readonly GQIColumn<string> _labelColumn;
    private readonly GQIColumn<double> _valueColumn;
    private readonly GQIColumn<double> _averageColumn;

    private GQIDMS _dms;
    private int _viewFilter;
    private string _timeSpan;

    public Distribution()
    {
        _labelColumn = new GQIStringColumn("Label");
        _valueColumn = new GQIDoubleColumn("Value");
        _averageColumn = new GQIDoubleColumn("Average");
    }

    public OnInitOutputArgs OnInit(OnInitInputArgs args)
    {
        _dms = args.DMS;
        return default;
    }

    public GQIArgument[] GetInputArguments()
    {
        return new GQIArgument[]
        {
            Report.Instance.ViewFilterArgument,
            Report.Instance.TimeSpanArgument,
        };
    }

    public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
    {
        _viewFilter = Report.Instance.GetViewFilter(args);
        _timeSpan = Report.Instance.GetTimeSpan(args);
        return default;
    }

    public GQIColumn[] GetColumns()
    {
        return new GQIColumn[]
        {
            _labelColumn,
            _valueColumn,
            _averageColumn,
        };
    }

    public GQIPage GetNextPage(GetNextPageInputArgs args)
    {
        var rows = GetRows(_timeSpan);
        return new GQIPage(rows);
    }

    private GQIRow CreateRow(string label, double value, double? average = null)
    {
        var cells = new[]
        {
            new GQICell { Value = label },
            new GQICell { Value = value },
            new GQICell { Value = average },
        };
        return new GQIRow(cells);
    }

    private GQIRow[] GetRows(string timeSpan)
    {
        switch (timeSpan)
        {
            case TimeSpans.DAY:
                return GetLast24Hours();
            case TimeSpans.WEEK:
                return GetLast7Days();
            case TimeSpans.MONTH:
                return GetLast30Days();
            default:
                return Array.Empty<GQIRow>();
        }
    }

    private GQIRow[] GetLast24Hours()
    {
        var valueRequest = GetData(ReportHistoryType.Last24Hours, ReportTimeslotType.Hour, ReportAverageType.NoAverage);
        var averageRequest = GetData(ReportHistoryType.LastWeek, ReportTimeslotType.Hour, ReportAverageType.Day);

        var labels = valueRequest.Labels;
        var values = valueRequest.DoubleValues;
        var averages = averageRequest.DoubleValues;

        var rows = new GQIRow[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            rows[i] = CreateRow(labels[i], values[i], averages[i]);
        }

        return rows;
    }

    private GQIRow[] GetLast7Days()
    {
        var valueRequest = GetData(ReportHistoryType.LastWeek, ReportTimeslotType.DayOfWeek, ReportAverageType.NoAverage);
        var averageRequest = GetData(ReportHistoryType.LastMonth, ReportTimeslotType.DayOfWeek, ReportAverageType.Week);

        var labels = valueRequest.Labels;
        var values = valueRequest.DoubleValues;
        var averages = averageRequest.DoubleValues;

        var rows = new GQIRow[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var weekDay = LabelToWeekDay(labels[i]);
            rows[i] = CreateRow(weekDay, values[i], averages[i]);
        }

        return rows;
    }

    private string LabelToWeekDay(string label)
    {
        switch (label)
        {
            case "1": return "Monday";
            case "2": return "Tuesday";
            case "3": return "Wednesday";
            case "4": return "Thursday";
            case "5": return "Friday";
            case "6": return "Saturday";
            case "7": return "Sunday";
            default: return label;
        }
    }

    private GQIRow[] GetLast30Days()
    {
        var valueRequest = GetData(ReportHistoryType.LastMonth, ReportTimeslotType.Day, ReportAverageType.NoAverage);

        var labels = valueRequest.Labels;
        var values = valueRequest.DoubleValues;

        var rows = new GQIRow[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            rows[i] = CreateRow(labels[i], values[i]);
        }

        return rows;
    }

    private ReportAlarmDistributionDataResponseMessage GetData(ReportHistoryType timeSpan, ReportTimeslotType timeSlot, ReportAverageType average)
    {
        var elementFilter = new ReportFilterInfo(ReportFilterType.Custom)
        {
            Keys = new[] { "1007918/65" },
        };
        var request = new GetReportAlarmDistributionDataMessage
        {
            Span = timeSpan,
            TimeslotSize = timeSlot,
            Average = average,
            IncludedSeverities = ReportIncludedSeverities.All,
            Options = ReportOptionFlags.IncludeDerivedElements | ReportOptionFlags.IncludeServices,
            Filter = elementFilter,
        };
        return _dms.SendMessage(request) as ReportAlarmDistributionDataResponseMessage;
    }
}
//---------------------------------
// DistributionLegend.cs
//---------------------------------

[GQIMetaData(Name = "Alarm report > Distribution legend")]
public sealed class DistributionLegend : IGQIDataSource, IGQIInputArguments
{
    public const string WEEKLY_AVG_LABEL = "7 day average";
    public const string MONTHLY_AVG_LABEL = "30 day average";

    public const string VALUE_TYPE = "VALUE";
    public const string AVERAGE_TYPE = "AVERAGE";

    private readonly GQIColumn<string> _labelColumn;
    private readonly GQIColumn<bool> _isAverageColumn;

    private string _timeSpan;

    public DistributionLegend()
    {
        _labelColumn = new GQIStringColumn("Label");
        _isAverageColumn = new GQIBooleanColumn("Is average");
    }

    public GQIArgument[] GetInputArguments()
    {
        return new[] { Report.Instance.TimeSpanArgument };
    }

    public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
    {
        _timeSpan = Report.Instance.GetTimeSpan(args);
        return default;
    }

    public GQIColumn[] GetColumns()
    {
        return new GQIColumn[]
        {
            _labelColumn,
            _isAverageColumn,
        };
    }

    public GQIPage GetNextPage(GetNextPageInputArgs args)
    {
        var rows = CreateRows(_timeSpan);
        return new GQIPage(rows);
    }

    private static GQIRow[] CreateRows(string timeSpan)
    {
        switch (timeSpan)
        {
            case TimeSpans.DAY:
                return new[]
                {
                    CreateRow(TimeSpans.DAY_LABEL, false),
                    CreateRow(WEEKLY_AVG_LABEL, true),
                };
            case TimeSpans.WEEK:
                return new[]
                {
                    CreateRow(TimeSpans.WEEK_LABEL, false),
                    CreateRow(MONTHLY_AVG_LABEL, true),
                };
            case TimeSpans.MONTH:
                return new[]
                {
                    CreateRow(TimeSpans.MONTH_LABEL, false),
                };
            default:
                return Array.Empty<GQIRow>();
        }
    }

    private static GQIRow CreateRow(string label, bool isAverage)
    {
        var cells = new[]
        {
            new GQICell { Value = label },
            new GQICell { Value = isAverage },
        };
        return new GQIRow(cells);
    }
}

//---------------------------------
// Events.cs
//---------------------------------

[GQIMetaData(Name = "Alarm report > Events")]
public sealed class Events : IGQIDataSource, IGQIOnInit, IGQIInputArguments
{
    private readonly GQIColumn<string> _nameColumn;
    private readonly GQIColumn<int> _timeoutColumn;
    private readonly GQIColumn<int> _warningColumn;
    private readonly GQIColumn<int> _minorColumn;
    private readonly GQIColumn<int> _majorColumn;
    private readonly GQIColumn<int> _criticalColumn;

    private GQIDMS _dms;

    private int _viewFilter;
    private string _timeSpan;

    public Events()
    {
        _nameColumn = new GQIStringColumn("Name");
        _timeoutColumn = new GQIIntColumn("Timeout");
        _warningColumn = new GQIIntColumn("Warning");
        _minorColumn = new GQIIntColumn("Minor");
        _majorColumn = new GQIIntColumn("Major");
        _criticalColumn = new GQIIntColumn("Critical");
    }

    public OnInitOutputArgs OnInit(OnInitInputArgs args)
    {
        _dms = args.DMS;
        return default;
    }

    public GQIArgument[] GetInputArguments()
    {
        return new GQIArgument[]
        {
            Report.Instance.ViewFilterArgument,
            Report.Instance.TimeSpanArgument,
        };
    }

    public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
    {
        _viewFilter = Report.Instance.GetViewFilter(args);
        _timeSpan = Report.Instance.GetTimeSpan(args);
        return default;
    }

    public GQIColumn[] GetColumns()
    {
        return new GQIColumn[]
        {
            _nameColumn,
            _timeoutColumn,
            _warningColumn,
            _minorColumn,
            _majorColumn,
            _criticalColumn,
        };
    }

    public GQIPage GetNextPage(GetNextPageInputArgs args)
    {
        var rows = GetRows(_timeSpan);
        return new GQIPage(rows);
    }

    private GQIRow[] GetRows(string timeSpan)
    {
        var data = GetAlarmCounts(timeSpan);

        var rows = new List<GQIRow>();
        foreach (var response in data)
        {
            var row = CreateRow(response);
            rows.Add(row);
        }
        return rows.ToArray();
    }

    private GQIRow CreateRow(ReportAlarmCountDataResponseMessage response)
    {
        var name = GetName(response);
        var cells = new[]
        {
            new GQICell { Value = name },
            new GQICell { Value = response.AmountTimeout },
            new GQICell { Value = response.AmountWarning },
            new GQICell { Value = response.AmountMinor },
            new GQICell { Value = response.AmountMajor },
            new GQICell { Value = response.AmountCritical },
        };
        return new GQIRow(cells);
    }

    private IEnumerable<ReportAlarmCountDataResponseMessage> GetAlarmCounts(string timeSpan)
    {
        var viewFilter = new ReportFilterInfo(ReportFilterType.View)
        {
            ViewID = _viewFilter,
        };
        var request = new GetReportAlarmCountDataMessage
        {
            Timespan = timeSpan,
            SortMethod = ReportTopSortType.Total,
            MaxAmount = 5,
            Options = ReportOptionFlags.IncludeDerivedElements | ReportOptionFlags.IncludeServices,
            Filter = viewFilter,
        };
        var responses = _dms.SendMessages(request);
        return responses.OfType<ReportAlarmCountDataResponseMessage>();
    }

    private string GetName(ReportAlarmCountDataResponseMessage response)
    {
        if (response.IsService)
            return GetServiceName(response.DataMinerID, response.ServiceID);
        else
            return GetElementName(response.DataMinerID, response.ElementID);
    }

    private string GetElementName(int dmaID, int elementID)
    {
        var request = GetLiteElementInfo.ByID(dmaID, elementID);
        var element = _dms.SendMessage(request) as LiteElementInfoEvent;
        return element.Name;
    }

    private string GetServiceName(int dmaID, int serviceID)
    {
        var request = GetLiteServiceInfo.ByID(dmaID, serviceID);
        var service = _dms.SendMessage(request) as LiteServiceInfoEvent;
        return service.Name;
    }
}
//---------------------------------
// Report.cs
//---------------------------------

internal sealed class Report
{
    private const int DEFAULT_VIEW_FILTER = -1;

    private static readonly Lazy<Report> _lazyInstance = new Lazy<Report>(() => new Report());

    private Report()
    {
        ViewFilterArgument = new GQIIntArgument("View filter")
        {
            IsRequired = false,
            DefaultValue = DEFAULT_VIEW_FILTER,
        };

        var timeSpanOptions = new[]
        {
            TimeSpans.DAY,
            TimeSpans.WEEK,
            TimeSpans.MONTH,
        };
        TimeSpanArgument = new GQIStringDropdownArgument("Time span", timeSpanOptions)
        {
            IsRequired = true,
            DefaultValue = TimeSpans.DAY,
        };
    }

    public static Report Instance => _lazyInstance.Value;

    public GQIArgument<int> ViewFilterArgument { get; }

    public GQIArgument<string> TimeSpanArgument { get; }

    public int GetViewFilter(OnArgumentsProcessedInputArgs argumentValues)
    {
        if (argumentValues.TryGetArgumentValue(ViewFilterArgument, out int viewFilter))
            return viewFilter;
        return DEFAULT_VIEW_FILTER;
    }

    public string GetTimeSpan(OnArgumentsProcessedInputArgs argumentValues)
    {
        return argumentValues.GetArgumentValue(TimeSpanArgument);
    }
}

//---------------------------------
// States.cs
//---------------------------------

[GQIMetaData(Name = "Alarm report > States")]
public sealed class States : IGQIDataSource, IGQIOnInit, IGQIInputArguments
{
    private readonly GQIColumn<string> _nameColumn;
    private readonly GQIColumn<double> _timeoutColumn;
    private readonly GQIColumn<double> _warningColumn;
    private readonly GQIColumn<double> _minorColumn;
    private readonly GQIColumn<double> _majorColumn;
    private readonly GQIColumn<double> _criticalColumn;

    private GQIDMS _dms;
    private int _viewFilter;
    private string _timeSpan;

    public States()
    {
        _nameColumn = new GQIStringColumn("Name");
        _timeoutColumn = new GQIDoubleColumn("Timeout");
        _warningColumn = new GQIDoubleColumn("Warning");
        _minorColumn = new GQIDoubleColumn("Minor");
        _majorColumn = new GQIDoubleColumn("Major");
        _criticalColumn = new GQIDoubleColumn("Critical");
    }

    public OnInitOutputArgs OnInit(OnInitInputArgs args)
    {
        _dms = args.DMS;
        return default;
    }

    public GQIArgument[] GetInputArguments()
    {
        return new GQIArgument[]
        {
            Report.Instance.ViewFilterArgument,
            Report.Instance.TimeSpanArgument,
        };
    }

    public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
    {
        _viewFilter = Report.Instance.GetViewFilter(args);
        _timeSpan = Report.Instance.GetTimeSpan(args);
        return default;
    }

    public GQIColumn[] GetColumns()
    {
        return new GQIColumn[]
        {
            _nameColumn,
            _timeoutColumn,
            _warningColumn,
            _minorColumn,
            _majorColumn,
            _criticalColumn,
        };
    }

    public GQIPage GetNextPage(GetNextPageInputArgs args)
    {
        var rows = GetRows(_timeSpan);
        return new GQIPage(rows);
    }

    private GQIRow[] GetRows(string timeSpan)
    {
        var data = GetAlarmCounts(timeSpan);

        var rows = new List<GQIRow>();
        foreach (var response in data)
        {
            var row = CreateRow(response);
            rows.Add(row);
        }
        return rows.ToArray();
    }

    private GQIRow CreateRow(ReportStateDataResponseMessage response)
    {
        var name = GetName(response);
        var cells = new[]
        {
            new GQICell { Value = name },
            new GQICell { Value = response.PercentageTimeout },
            new GQICell { Value = response.PercentageWarning },
            new GQICell { Value = response.PercentageMinor },
            new GQICell { Value = response.PercentageMajor },
            new GQICell { Value = response.PercentageCritical },
        };
        return new GQIRow(cells);
    }

    private IEnumerable<ReportStateDataResponseMessage> GetAlarmCounts(string timeSpan)
    {
        var viewFilter = new ReportFilterInfo(ReportFilterType.View)
        {
            ViewID = _viewFilter,
        };
        var request = new GetReportStateDataMessage
        {
            Timespan = timeSpan,
            SortMethod = ReportTopSortType.Total,
            MaxAmount = 5,
            Options = ReportOptionFlags.IncludeDerivedElements | ReportOptionFlags.IncludeServices,
            Filter = viewFilter,
        };
        var responses = _dms.SendMessages(request);
        return responses.OfType<ReportStateDataResponseMessage>();
    }

    private string GetName(ReportStateDataResponseMessage response)
    {
        if (response.IsService)
            return GetServiceName(response.DataMinerID, response.ServiceID);
        else
            return GetElementName(response.DataMinerID, response.ElementID);
    }

    private string GetElementName(int dmaID, int elementID)
    {
        var request = GetLiteElementInfo.ByID(dmaID, elementID);
        var element = _dms.SendMessage(request) as LiteElementInfoEvent;
        return element.Name;
    }

    private string GetServiceName(int dmaID, int serviceID)
    {
        var request = GetLiteServiceInfo.ByID(dmaID, serviceID);
        var service = _dms.SendMessage(request) as LiteServiceInfoEvent;
        return service.Name;
    }
}
//---------------------------------
// TimeSpans.cs
//---------------------------------

[GQIMetaData(Name = "Alarm report > Time spans")]
public sealed class TimeSpans : IGQIDataSource
{
    public const string DAY = "DAY";
    public const string WEEK = "WEEK";
    public const string MONTH = "MONTH";

    public const string DAY_LABEL = "Last 24 hours";
    public const string WEEK_LABEL = "Last 7 days";
    public const string MONTH_LABEL = "Last 30 days";

    private readonly GQIColumn<string> _labelColumn;
    private readonly GQIColumn<string> _valueColumn;

    public TimeSpans()
    {
        _labelColumn = new GQIStringColumn("Label");
        _valueColumn = new GQIStringColumn("Value");
    }

    public GQIColumn[] GetColumns()
    {
        return new GQIColumn[]
        {
            _labelColumn,
            _valueColumn,
        };
    }

    public GQIPage GetNextPage(GetNextPageInputArgs args)
    {
        var rows = new[]
        {
            CreateRow(DAY_LABEL, DAY),
            CreateRow(WEEK_LABEL, WEEK),
            CreateRow(MONTH_LABEL, MONTH),
        };
        return new GQIPage(rows);
    }

    private GQIRow CreateRow(string label, string value)
    {
        var cells = new[]
        {
            new GQICell { Value = label },
            new GQICell { Value = value },
        };
        return new GQIRow(cells);
    }
}
