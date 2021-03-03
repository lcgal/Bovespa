/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NodaTime;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Data.Custom.TradingEconomics
{
    /// <summary>
    /// Represents the Trading Economics Calendar information:
    /// The economic calendar covers around 1600 events for more than 150 countries a month.
    /// https://docs.tradingeconomics.com/#events
    /// </summary>
    public class TradingEconomicsCalendar : BaseData
    {
        // Filtered calendar event name
        private string _filteredEvent;
        private string _originalEvent;
        private const string _baseUrl = "https://api.tradingeconomics.com";

        /// <summary>
        /// Determines if the API key has been set
        /// </summary>
        public static bool IsAuthCodeSet { get; private set; }

        /// <summary>
        /// API key for Trading Economics
        /// </summary>
        public static string AuthCode { get; set; } = string.Empty;

        /// <summary>
        /// Unique calendar ID used by Trading Economics
        /// </summary>
        [JsonProperty(PropertyName = "CalendarId")]
        public string CalendarId { get; set; }

        /// <summary>
        /// Release time and date in UTC
        /// </summary>
        [JsonProperty(PropertyName = "Date"), JsonConverter(typeof(TradingEconomicsDateTimeConverter))]
        public override DateTime EndTime
        {
            get { return Time; }
            set { Time = value; }
        }

        /// <summary>
        /// Country name
        /// </summary>
        [JsonProperty(PropertyName = "Country")]
        public string Country { get; set; }

        /// <summary>
        /// Indicator category name
        /// </summary>
        [JsonProperty(PropertyName = "Category")]
        public string Category { get; set; }

        /// <summary>
        /// Specific event name in the calendar
        /// </summary>
        [JsonProperty(PropertyName = "Event")]
        public string Event
        {
            get { return _filteredEvent; }
            set
            {
                _originalEvent = value;
                _filteredEvent = TradingEconomicsEventFilter.FilterEvent(value);
            }
        }

        /// <summary>
        /// Raw event name as provided by Trading Economics
        /// </summary>
        [JsonIgnore]
        public string EventRaw
        {
            get { return _originalEvent; }
            set { _originalEvent = value; }
        }

        /// <summary>
        /// The period for which released data refers to
        /// </summary>
        [JsonProperty(PropertyName = "Reference")]
        public string Reference { get; set; }

        /// <summary>
        /// Source of data
        /// </summary>
        [JsonProperty(PropertyName = "Source")]
        public string Source { get; set; }

        /// <summary>
        /// Latest released value
        /// </summary>
        [JsonProperty(PropertyName = "Actual")]
        public decimal? Actual { get; set; }

        /// <summary>
        /// Value for the previous period after the revision (if revision is applicable)
        /// </summary>
        [JsonProperty(PropertyName = "Previous")]
        public decimal? Previous { get; set; }

        /// <summary>
        /// Average forecast among a representative group of economists
        /// </summary>
        [JsonProperty(PropertyName = "Forecast")]
        public decimal? Forecast { get; set; }

        /// <summary>
        /// TradingEconomics own projections
        /// </summary>
        [JsonProperty(PropertyName = "TEForecast")]
        public decimal? TradingEconomicsForecast { get; set; }

        /// <summary>
        /// 0 indicates that the time of the event is known,
        /// 1 indicates that we only know the date of event, the exact time of event is unknown
        /// </summary>
        [JsonProperty(PropertyName = "DateSpan")]
        public string DateSpan { get; set; }

        /// <summary>
        /// Importance of a TradingEconomics information
        /// </summary>
        [JsonProperty(PropertyName = "Importance")]
        public TradingEconomicsImportance Importance { get; set; }

        /// <summary>
        /// Time when new data was inserted or changed
        /// </summary>
        [JsonProperty(PropertyName = "LastUpdate"), JsonConverter(typeof(TradingEconomicsDateTimeConverter))]
        public DateTime LastUpdate { get; set; }

        /// <summary>
        /// Value reported in the previous period after revision
        /// </summary>
        /// <remarks>
        /// If there is no revision field remains empty
        /// </remarks>
        [JsonProperty(PropertyName = "Revised")]
        public decimal? Revised { get; set; }

        /// <summary>
        /// Country's original name
        /// </summary>
        [JsonProperty(PropertyName = "OCountry")]
        public string OCountry { get; set; }

        /// <summary>
        /// Category's original name
        /// </summary>
        [JsonProperty(PropertyName = "OCategory")]
        public string OCategory { get; set; }

        /// <summary>
        /// Unique ticker used by Trading Economics
        /// </summary>
        [JsonProperty(PropertyName = "Ticker")]
        public string Ticker { get; set; }

        /// <summary>
        /// Indicates whether the Actual, Previous, Forecast, TradingEconomicsForecast fields are reported as percent values
        /// </summary>
        public bool IsPercentage { get; set; }

        /// <summary>
        /// Return the Subscription Data Source gained from the URL
        /// </summary>
        /// <param name="config">Configuration object</param>
        /// <param name="date">Date of this source file</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>Subscription Data Source.</returns>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            var country = config.Symbol.Value.ToLowerInvariant()
                .Split(new[] { TradingEconomics.Calendar.Delimiter }, StringSplitOptions.None)
                .First();

            if (isLiveMode)
            {
                var liveSource = $"{_baseUrl}/calendar/country/{country.Replace("-", "%20")}/{date:yyyy-MM-dd}/{date.AddDays(1):yyyy-MM-dd}?c={AuthCode}&format=json";
                return new SubscriptionDataSource(liveSource, SubscriptionTransportMedium.RemoteFile, FileFormat.Collection);
            }

            country = country.Replace("-", "");
            var source = Path.Combine(Globals.DataFolder, "alternative", "trading-economics", "calendar", $"{country}.csv");
            return new SubscriptionDataSource(source, SubscriptionTransportMedium.LocalFile, FileFormat.Csv);
        }

        /// <summary>
        /// Reader converts each line of the data source into BaseData objects.
        /// </summary>
        /// <param name="config">Subscription data config setup object</param>
        /// <param name="line">Line from the data source</param>
        /// <param name="date">Date of the requested data</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>
        /// TradingEconomicsCalendar or BaseDataCollection object containing TradingEconomicsCalendar as its Data.
        /// </returns>
        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            var ticker = config.Symbol.Value.Split(new[] { TradingEconomics.Calendar.Delimiter }, StringSplitOptions.None)[1].Replace('-', ' ');

            if (isLiveMode)
            {
                var instances = ProcessAPIResponse(line);
                foreach (var item in instances)
                {
                    item.Symbol = config.Symbol;
                }
                return new BaseDataCollection(DateTime.UtcNow, config.Symbol, instances.Where(x => ticker == x.Ticker));
            }

            var i = 0;
            var csv = line.ToCsvData(18);
            var instance = new TradingEconomicsCalendar();

            instance.EndTime = Parse.DateTimeExact(csv[i++], "yyyyMMdd HH:mm:ss");
            instance.LastUpdate = Parse.DateTimeExact(csv[i++], "yyyyMMdd HH:mm:ss");
            if (instance.EndTime < date)
            {
                return null;
            }

            instance.Ticker = csv[i++];
            if (ticker != instance.Ticker.Replace('-', ' ').ToUpperInvariant())
            {
                return null;
            }

            // Initialize with long form instead of using shorthand `var something = new Obj { X = ..., Y = ..., Z = ... };`
            // so that we can more easily debug and step through the code should the parsing fail.
            instance.Actual = csv[i++].IfNotNullOrEmpty<decimal?>(x => Parse.Decimal(x));
            instance.CalendarId = csv[i++];
            instance.Category = csv[i++];
            instance.Country = csv[i++];
            instance.DataType = (MarketDataType)Parse.Int(csv[i++]);
            instance.DateSpan = csv[i++];
            instance.Event = csv[i++].Trim('"');
            instance.Forecast = csv[i++].IfNotNullOrEmpty<decimal?>(x => Parse.Decimal(x));
            instance.Importance = (TradingEconomicsImportance)Parse.Int(csv[i++]);
            instance.IsPercentage = csv[i++] == "true";
            instance.OCategory = csv[i++];
            instance.OCountry = csv[i++];
            instance.Previous = csv[i++].IfNotNullOrEmpty<decimal?>(x => Parse.Decimal(x));
            instance.Reference = csv[i++].Trim('"');
            instance.Revised = csv[i++].IfNotNullOrEmpty<decimal?>(x => Parse.Decimal(x));
            instance.Source = csv[i++].Trim('"');
            i++;
            instance.TradingEconomicsForecast = csv[i++].IfNotNullOrEmpty<decimal?>(x => Parse.Decimal(x));
            instance.Symbol = config.Symbol;

            return instance;
        }

        /// <summary>
        /// Clones the data. This is required for some custom data
        /// </summary>
        /// <returns>A new cloned instance</returns>
        public override BaseData Clone()
        {
            return new TradingEconomicsCalendar
            {
                CalendarId = CalendarId,
                EndTime = EndTime,
                Country = Country,
                Category = Category,
                Event = EventRaw, // Set as original in order to preserve the raw event
                Reference = Reference,
                Source = Source,
                Actual = Actual,
                Previous = Previous,
                Forecast = Forecast,
                TradingEconomicsForecast = TradingEconomicsForecast,
                DateSpan = DateSpan,
                Importance = Importance,
                LastUpdate = LastUpdate,
                Revised = Revised,
                OCountry = Country,
                OCategory = OCategory,
                Ticker = Ticker,
                IsPercentage = IsPercentage,


                Symbol = Symbol,
                Time = Time
            };
        }

        /// <summary>
        /// Formats a string with the Trading Economics Calendar information.
        /// </summary>
        public override string ToString()
        {
            return Invariant($"{Ticker} ({Country} - {Category}): {Event} : Importance.{Importance}");
        }

        /// <summary>
        /// Convert this instance to a CSV file
        /// </summary>
        /// <returns>string as CSV</returns>
        public string ToCsv()
        {
            return string.Join(",",
                EndTime.ToStringInvariant("yyyyMMdd HH:mm:ss"),
                LastUpdate.ToStringInvariant("yyyyMMdd HH:mm:ss"),
                Ticker,
                Actual.ToStringInvariant(),
                CalendarId,
                Category,
                Country,
                (int)DataType,
                DateSpan,
                $"\"{EventRaw}\"",
                Forecast.ToStringInvariant(),
                (int)Importance,
                IsPercentage.ToStringInvariant().ToLowerInvariant(),
                OCategory,
                OCountry,
                Previous.ToStringInvariant(),
                $"\"{Reference}\"",
                Revised.ToStringInvariant(),
                $"\"{Source}\"",
                string.Empty,
                TradingEconomicsForecast.ToStringInvariant()
            );
        }

        /// <summary>
        /// Specifies the data time zone for this data type. This is useful for custom data types
        /// </summary>
        /// <returns>The <see cref="DateTimeZone"/> of this data type</returns>
        public override DateTimeZone DataTimeZone()
        {
            return TimeZones.Utc;
        }

        /// <summary>
        /// Sets the Trading Economics API key.
        /// </summary>
        /// <param name="authCode">The Trading Economics API key</param>
        public static void SetAuthCode(string authCode)
        {
            if (string.IsNullOrWhiteSpace(authCode)) return;

            AuthCode = authCode;
            IsAuthCodeSet = true;
        }

        /// <summary>
        /// Parses the raw Trading Economics calendar API result
        /// </summary>
        /// <param name="content">Contents of returned data</param>
        /// <returns>List of instances of the current class</returns>
        public static List<TradingEconomicsCalendar> ProcessAPIResponse(string content)
        {
            var rawCollection = JsonConvert.DeserializeObject<JArray>(content);

            foreach (var rawData in rawCollection)
            {
                var inPercentage = rawData["Actual"].Value<string>().Contains("%");
                var ticker = rawData["Ticker"].Value<string>();
                var country = rawData["Country"].Value<string>();

                if (string.IsNullOrEmpty(ticker))
                {
                    rawData["Ticker"] = CountryToCurrencyCode(country) + " CALENDAR";
                }

                rawData["IsPercentage"] = inPercentage;
                rawData["Actual"] = ParseDecimal(rawData["Actual"].Value<string>(), inPercentage);
                rawData["Previous"] = ParseDecimal(rawData["Previous"].Value<string>(), inPercentage);
                rawData["Forecast"] = ParseDecimal(rawData["Forecast"].Value<string>(), inPercentage);
                rawData["TEForecast"] = ParseDecimal(rawData["TEForecast"].Value<string>(), inPercentage);
                rawData["Revised"] = ParseDecimal(rawData["Revised"].Value<string>(), inPercentage);

                ((JObject)rawData).Remove("Symbol");
            }

            return rawCollection.ToObject<List<TradingEconomicsCalendar>>();
        }

        /// <summary>
        /// Parse decimal from calendar data
        /// </summary>
        /// <param name="value">Value to parse</param>
        /// <param name="inPercent">Is the value a percentage</param>
        /// <returns>Nullable decimal</returns>
        /// <remarks>Will be null when we can't parse the data reliably</remarks>
        public static decimal? ParseDecimal(string value, bool inPercent)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // Remove dollar signs from values
            // Remove (P) and (R) from values
            // Edge cases: values are reported as XYZ.5.1B, -4-XYZ
            var newFigure = value.Replace("$", "")
                .Replace("(P)", "")
                .Replace("(R)", "")
                .Replace("--", "-")
                .Replace(".5.1", ".5")
                .Replace("-1-", "-")
                .Replace("-2-", "-")
                .Replace("-3-", "-")
                .Replace("-4-", "-")
                .Replace("-5-", "-")
                .Replace("-6-", "-")
                .Replace("-7-", "-")
                .Replace("-8-", "-")
                .Replace("-9-", "-");

            if (newFigure.EndsWith("."))
            {
                newFigure = newFigure.Substring(0, newFigure.Length - 1);
            }

            var inTrillions = newFigure.EndsWith("T");
            var inBillions = newFigure.EndsWith("B");
            var inMillions = newFigure.EndsWith("M");
            var inThousands = newFigure.EndsWith("K");

            // Finally, remove any alphabetical characters from the string before we parse
            newFigure = Regex.Replace(newFigure, "[^0-9.+-]", "");

            while (Regex.IsMatch(newFigure, @"(\.[0-9]+\.)"))
            {
                newFigure = newFigure.Substring(0, newFigure.Length - 1);
            }

            if (string.IsNullOrWhiteSpace(newFigure))
            {
                // U.S. Presidential election is unparsable as decimal.
                // Other events similar to it might exist as well.
                return null;
            }

            // Return null If we can't parse the result as is
            decimal finalFigure;
            if (!decimal.TryParse(newFigure, NumberStyles.Any, CultureInfo.InvariantCulture, out finalFigure))
            {
                Log.Error($"TradingEconomicsDownloader.ParseDecimal(): Failed to parse the figure {value}. Final form before parsing: {newFigure}");
                return null;
            }

            if (inPercent)
            {
                return finalFigure / 100m;
            }
            if (inTrillions)
            {
                return finalFigure * 1000000000000m;
            }
            if (inBillions)
            {
                return finalFigure * 1000000000m;
            }
            if (inMillions)
            {
                return finalFigure * 1000000m;
            }
            if (inThousands)
            {
                return finalFigure * 1000m;
            }

            return finalFigure;
        }

        /// <summary>
        /// Converts country name to currency code (ISO 4217)
        /// </summary>
        /// <param name="country">Country name</param>
        /// <returns>ISO 4217 currency code</returns>
        public static string CountryToCurrencyCode(string country)
        {
            var regions = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(x => new RegionInfo(x.LCID));
            var countryLower = country.ToLowerInvariant();
            return regions.FirstOrDefault(region => region.EnglishName.ToLowerInvariant().Contains(countryLower))?.ISOCurrencySymbol;
        }
    }

    /// <summary>
    /// Importance of a TradingEconomics information
    /// </summary>
    public enum TradingEconomicsImportance
    {
        /// <summary>
        /// Low importance
        /// </summary>
        [JsonProperty(PropertyName = "low")]
        Low,

        /// <summary>
        /// Medium importance
        /// </summary>
        [JsonProperty(PropertyName = "medium")]
        Medium,

        /// <summary>
        /// High importance
        /// </summary>
        [JsonProperty(PropertyName = "high")]
        High
    }
}