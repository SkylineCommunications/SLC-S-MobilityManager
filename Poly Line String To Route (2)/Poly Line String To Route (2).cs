namespace PolyLineToRoute
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Analytics.GenericInterface;

    [GQIMetaData(Name = "Poly Line String To Route Tarik")]
    public sealed class PolyLineToRoute : IGQIDataSource, IGQIOnInit, IGQIInputArguments, IGQIOnPrepareFetch
    {
        private readonly GQIStringArgument _lineStringArgument = new GQIStringArgument("Line String") { IsRequired = true };

        private GQIDMS _dms;

        private string _lineString;

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;

            return default;
        }

        public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
        {
            return default;
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[] { _lineStringArgument };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            _lineString = args.GetArgumentValue(_lineStringArgument);

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

            if (string.IsNullOrEmpty(_lineString))
            {
                return rows;
            }

            var geoPoints = GeoParser.Parse(_lineString);
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

        var pairs = input
            .Split(new[] { "],[" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Replace("[", string.Empty).Replace("]", string.Empty))
            .Select(p => p.Split(',')
                          .Select(x => double.Parse(x.Trim(), System.Globalization.CultureInfo.InvariantCulture))
                          .ToArray())
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
