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
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using QuantConnect.Securities;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Algorithm backtest task information packet.
    /// </summary>
    public class BacktestNodePacket : AlgorithmNodePacket
    {
        // default random id, static so its one per process
        private static readonly string DefaultId
            = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        /// <summary>
        /// Name of the backtest as randomly defined in the IDE.
        /// </summary>
        [JsonProperty(PropertyName = "sName")]
        public string Name = "";

        /// <summary>
        /// BacktestId / Algorithm Id for this task
        /// </summary>
        [JsonProperty(PropertyName = "sBacktestID")]
        public string BacktestId = DefaultId;

        /// <summary>
        /// Optimization Id for this task
        /// </summary>
        [JsonProperty(PropertyName = "sOptimizationID")]
        public string OptimizationId;

        /// <summary>
        /// Backtest start-date as defined in the Initialize() method.
        /// </summary>
        [JsonProperty(PropertyName = "dtPeriodStart")]
        public DateTime? PeriodStart;

        /// <summary>
        /// Backtest end date as defined in the Initialize() method.
        /// </summary>
        [JsonProperty(PropertyName = "dtPeriodFinish")]
        public DateTime? PeriodFinish;

        /// <summary>
        /// Estimated number of trading days in this backtest task based on the start-end dates.
        /// </summary>
        [JsonProperty(PropertyName = "iTradeableDates")]
        public int TradeableDates = 0;

        /// <summary>
        /// The initial breakpoints for debugging, if any
        /// </summary>
        [JsonProperty(PropertyName = "aBreakpoints")]
        public List<Breakpoint> Breakpoints = new List<Breakpoint>();

        /// <summary>
        /// The initial Watchlist for debugging, if any
        /// </summary>
        [JsonProperty(PropertyName = "aWatchlist")]
        public List<string> Watchlist = new List<string>();

        /// <summary>
        /// True, if this is a debugging backtest
        /// </summary>
        public bool IsDebugging => Breakpoints.Any();

        /// <summary>
        /// Optional initial cash amount if set
        /// </summary>
        public CashAmount? CashAmount;

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public BacktestNodePacket()
            : base(PacketType.BacktestNode)
        {
            Controls = new Controls
            {
                MinuteLimit = 500,
                SecondLimit = 100,
                TickLimit = 30
            };
        }

        /// <summary>
        /// Initialize the backtest task packet.
        /// </summary>
        public BacktestNodePacket(int userId, int projectId, string sessionId, byte[] algorithmData, decimal startingCapital, string name, UserPlan userPlan = UserPlan.Free) 
            : this (userId, projectId, sessionId, algorithmData, name, userPlan, new CashAmount(startingCapital, Currencies.USD))
        {
        }

        /// <summary>
        /// Initialize the backtest task packet.
        /// </summary>
        public BacktestNodePacket(int userId, int projectId, string sessionId, byte[] algorithmData, string name, UserPlan userPlan = UserPlan.Free, CashAmount? startingCapital = null)
            : base(PacketType.BacktestNode)
        {
            UserId = userId;
            Algorithm = algorithmData;
            SessionId = sessionId;
            ProjectId = projectId;
            UserPlan = userPlan;
            Name = name;
            CashAmount = startingCapital;
            Language = Language.CSharp;
            Controls = new Controls
            {
                MinuteLimit = 500,
                SecondLimit = 100,
                TickLimit = 30
            };
        }
    }
}
