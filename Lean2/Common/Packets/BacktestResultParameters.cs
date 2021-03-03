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
 *
*/

using System;
using System.Collections.Generic;
using QuantConnect.Orders;
using QuantConnect.Statistics;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Defines the parameters for <see cref="BacktestResult"/>
    /// </summary>
    public class BacktestResultParameters : BaseResultParameters
    {
        /// <summary>
        /// Rolling window detailed statistics.
        /// </summary>
        public Dictionary<string, AlgorithmPerformance> RollingWindow { get; set; }

        /// <summary>
        /// Rolling window detailed statistics.
        /// </summary>
        public AlgorithmPerformance TotalPerformance { get; set; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        public BacktestResultParameters(IDictionary<string, Chart> charts,
            IDictionary<int, Order> orders,
            IDictionary<DateTime, decimal> profitLoss,
            IDictionary<string, string> statistics,
            IDictionary<string, string> runtimeStatistics,
            Dictionary<string, AlgorithmPerformance> rollingWindow,
            List<OrderEvent> orderEvents,
            AlgorithmPerformance totalPerformance = null,
            AlphaRuntimeStatistics alphaRuntimeStatistics = null)
        {
            Charts = charts;
            Orders = orders;
            ProfitLoss = profitLoss;
            Statistics = statistics;
            RuntimeStatistics = runtimeStatistics;
            RollingWindow = rollingWindow;
            OrderEvents = orderEvents;
            TotalPerformance = totalPerformance;
            AlphaRuntimeStatistics = alphaRuntimeStatistics;
        }
    }
}
