﻿/*
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

using System;
using System.Collections.Generic;
using System.Linq;
using Deedle;
using Python.Runtime;
using QuantConnect.Packets;
using QuantConnect.Util;

namespace QuantConnect.Report.ReportElements
{
    internal sealed class DrawdownReportElement : ChartReportElement
    {
        private LiveResult _live;
        private BacktestResult _backtest;

        /// <summary>
        /// Create a new plot of the top N worst drawdown durations
        /// </summary>
        /// <param name="name">Name of the widget</param>
        /// <param name="key">Location of injection</param>
        /// <param name="backtest">Backtest result object</param>
        /// <param name="live">Live result object</param>
        public DrawdownReportElement(string name, string key, BacktestResult backtest, LiveResult live)
        {
            _live = live;
            _backtest = backtest;
            Name = name;
            Key = key;
        }

        /// <summary>
        /// Generate the top N drawdown plot using the python libraries.
        /// </summary>
        public override string Render()
        {
            var backtestPoints = ResultsUtil.EquityPoints(_backtest);
            var livePoints = ResultsUtil.EquityPoints(_live);

            var liveSeries = new Series<DateTime, double>(livePoints.Keys, livePoints.Values);
            var strategySeries = DrawdownCollection.NormalizeResults(_backtest, _live);

            var seriesUnderwaterPlot = DrawdownCollection.GetUnderwater(strategySeries).DropMissing();
            var liveUnderwaterPlot = backtestPoints.Count == 0 ? seriesUnderwaterPlot : seriesUnderwaterPlot.After(backtestPoints.Last().Key);
            var drawdownCollection = DrawdownCollection.FromResult(_backtest, _live, periods: 5);

            var base64 = "";
            using (Py.GIL())
            {
                var backtestList = new PyList();

                if (liveUnderwaterPlot.IsEmpty)
                {
                    backtestList.Append(seriesUnderwaterPlot.Keys.ToList().ToPython());
                    backtestList.Append(seriesUnderwaterPlot.Values.ToList().ToPython());
                }
                else
                {
                    backtestList.Append(seriesUnderwaterPlot.Before(liveUnderwaterPlot.FirstKey()).Keys.ToList().ToPython());
                    backtestList.Append(seriesUnderwaterPlot.Before(liveUnderwaterPlot.FirstKey()).Values.ToList().ToPython());
                }

                var liveList = new PyList();
                liveList.Append(liveUnderwaterPlot.Keys.ToList().ToPython());
                liveList.Append(liveUnderwaterPlot.Values.ToList().ToPython());

                var worstList = new PyList();
                var previousDrawdownPeriods = new List<KeyValuePair<DateTime, DateTime>>();

                foreach (var group in drawdownCollection.Drawdowns)
                {
                    // Skip drawdown periods that are overlapping
                    if (previousDrawdownPeriods.Where(kvp => (group.Start >= kvp.Key && group.Start <= kvp.Value) || (group.End >= kvp.Key && group.End <= kvp.Value)).Any())
                    {
                        continue;
                    }

                    var worst = new PyDict();
                    worst.SetItem("Begin", group.Start.ToPython());
                    worst.SetItem("End", group.End.ToPython());
                    worst.SetItem("Total", group.PeakToTrough.ToPython());

                    worstList.Append(worst);
                    previousDrawdownPeriods.Add(new KeyValuePair<DateTime, DateTime>(group.Start, group.End));
                }

                base64 = Charting.GetDrawdown(backtestList, liveList, worstList);
            }

            return base64;
        }
    }
}