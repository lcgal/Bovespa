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
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Tests.Engine.DataFeeds;

namespace QuantConnect.Tests.Indicators
{
    /// <summary>
    ///     Test class for QuantConnect.Indicators.Indicator
    /// </summary>
    [TestFixture]
    public class IndicatorTests
    {
        [Test]
        public void NameSaves()
        {
            // just testing that we get the right name out
            const string name = "name";
            var target = new TestIndicator(name);
            Assert.AreEqual(name, target.Name);
        }

        [Test]
        public void UpdatesProperly()
        {
            // we want to make sure the initialized value is the default value
            // for a datapoint, and also verify the our indicator updates as we
            // expect it to, in this case, it should return identity
            var target = new TestIndicator();

            Assert.AreEqual(DateTime.MinValue, target.Current.Time);
            Assert.AreEqual(0m, target.Current.Value);

            var time = DateTime.UtcNow;
            var data = new IndicatorDataPoint(time, 1m);

            target.Update(data);
            Assert.AreEqual(1m, target.Current.Value);

            target.Update(new IndicatorDataPoint(time.AddMilliseconds(1), 2m));
            Assert.AreEqual(2m, target.Current.Value);
        }

        [Test]
        public void ThrowsOnDifferentDataType()
        {
            var target = new TestIndicator();
            Assert.Throws<ArgumentException>(() =>
            {
                target.Update(new Tick());
            }, "expected to be of type");
        }

        [Test]
        public void PassesOnDuplicateTimes()
        {
            var target = new TestIndicator();

            var time = DateTime.UtcNow;

            const decimal value1 = 1m;
            var data = new IndicatorDataPoint(time, value1);
            target.Update(data);
            Assert.AreEqual(value1, target.Current.Value);

            // this won't update because we told it to ignore duplicate
            // data based on time
            target.Update(data);
            Assert.AreEqual(value1, target.Current.Value);
        }

        [Test]
        public void SortsTheSameAsDecimalDescending()
        {
            int count = 100;
            var targets = Enumerable.Range(0, count)
                .Select(x => new TestIndicator(x.ToString(CultureInfo.InvariantCulture)))
                .ToList();

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Update(DateTime.Today, i);
            }

            var expected = Enumerable.Range(0, count)
                .Select(x => (decimal)x)
                .OrderByDescending(x => x)
                .ToList();

            var actual = targets.OrderByDescending(x => x).ToList();
            foreach (var pair in expected.Zip<decimal, TestIndicator, Tuple<decimal, TestIndicator>>(actual, Tuple.Create))
            {
                Assert.AreEqual(pair.Item1, pair.Item2.Current.Value);
            }
        }

        [Test]
        public void SortsTheSameAsDecimalAsecending()
        {
            int count = 100;
            var targets = Enumerable.Range(0, count).Select(x => new TestIndicator(x.ToString(CultureInfo.InvariantCulture))).ToList();
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Update(DateTime.Today, i);
            }

            var expected = Enumerable.Range(0, count).Select(x => (decimal)x).OrderBy(x => x).ToList();
            var actual = targets.OrderBy(x => x).ToList();
            foreach (var pair in expected.Zip<decimal, TestIndicator, Tuple<decimal, TestIndicator>>(actual, Tuple.Create))
            {
                Assert.AreEqual(pair.Item1, pair.Item2.Current.Value);
            }
        }

        [Test]
        public void ComparisonFunctions()
        {
            TestComparisonOperators<int>();
            TestComparisonOperators<long>();
            TestComparisonOperators<float>();
            TestComparisonOperators<double>();
        }

        [Test]
        public void EqualsMethodShouldNotThrowExceptions()
        {
            var indicator = new TestIndicator();
            var res = true;
            try
            {
                res = indicator.Equals(new Exception(""));
            }
            catch (InvalidCastException)
            {
                Assert.Fail();
            }
            Assert.IsFalse(res);
        }

        [Test]
        public void IndicatorMustBeEqualToItself()
        {
            var indicators = typeof(Indicator).Assembly.GetTypes()
                .Where(t => t.BaseType.Name != "CandlestickPattern" && !t.Name.StartsWith("<"))
                .OrderBy(t => t.Name)
                .ToList();

            var counter = 0;
            object instantiatedIndicator;
            foreach (var indicator in indicators)
            {
                try
                {
                    instantiatedIndicator = Activator.CreateInstance(indicator, new object[] {10});
                    counter++;
                }
                catch (Exception e)
                {
                    // Some indicators will fail because they don't have a single-parameter constructor.
                    continue;
                }

                Assert.IsTrue(instantiatedIndicator.Equals(instantiatedIndicator));
                var anotherInstantiatedIndicator = Activator.CreateInstance(indicator, new object[] { 10 });
                Assert.IsFalse(instantiatedIndicator.Equals(anotherInstantiatedIndicator));
            }
            Log.Trace($"{counter} indicators out of {indicators.Count} were tested.");
        }

        [Test]
        public void IndicatorsOfDifferentTypeDiplaySameCurrentTime()
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var spy = algorithm.AddEquity("SPY");

            var indicatorTimeList = new List<DateTime>();
            // RSI is a DataPointIndicator
            algorithm.RSI(spy.Symbol, 14).Updated += (_, e) => indicatorTimeList.Add(e.EndTime);
            // STO is a BarIndicator
            algorithm.STO(spy.Symbol, 14, 2, 2).Updated += (_, e) => indicatorTimeList.Add(e.EndTime);
            // MFI is a TradeBarIndicator
            algorithm.MFI(spy.Symbol, 14).Updated += (_, e) => indicatorTimeList.Add(e.EndTime);

            var consolidators = spy.Subscriptions.SelectMany(x => x.Consolidators).ToList();
            Assert.AreEqual(3, consolidators.Count);   // One consolidator for each indicator

            var bars = new[] { 30, 31 }.Select(d =>
                new TradeBar(new DateTime(2020, 03, 04, 9, d, 0),
                             spy.Symbol, 100, 100, 100, 100, 1000));

            foreach (var bar in bars)
            {
                foreach (var consolidator in consolidators)
                {
                    consolidator.Update(bar);
                }
            }

            // All indicators should have the same EndTime
            Assert.AreEqual(1, indicatorTimeList.Distinct().Count());
        }

        private static void TestComparisonOperators<TValue>()
        {
            var indicator = new TestIndicator();
            TestOperator(indicator, default(TValue), "GreaterThan", true, false);
            TestOperator(indicator, default(TValue), "GreaterThan", false, false);
            TestOperator(indicator, default(TValue), "GreaterThanOrEqual", true, true);
            TestOperator(indicator, default(TValue), "GreaterThanOrEqual", false, true);
            TestOperator(indicator, default(TValue), "LessThan", true, false);
            TestOperator(indicator, default(TValue), "LessThan", false, false);
            TestOperator(indicator, default(TValue), "LessThanOrEqual", true, true);
            TestOperator(indicator, default(TValue), "LessThanOrEqual", false, true);
            TestOperator(indicator, default(TValue), "Equality", true, true);
            TestOperator(indicator, default(TValue), "Equality", false, true);
            TestOperator(indicator, default(TValue), "Inequality", true, false);
            TestOperator(indicator, default(TValue), "Inequality", false, false);
        }

        private static void TestOperator<TIndicator, TValue>(TIndicator indicator, TValue value, string opName, bool tvalueIsFirstParm, bool expected)
        {
            var method = GetOperatorMethodInfo<TValue>(opName, tvalueIsFirstParm ? 0 : 1);
            var ctIndicator = Expression.Constant(indicator);
            var ctValue = Expression.Constant(value);
            var call = tvalueIsFirstParm ? Expression.Call(method, ctValue, ctIndicator) : Expression.Call(method, ctIndicator, ctValue);
            var lamda = Expression.Lambda<Func<bool>>(call);
            var func = lamda.Compile();
            Assert.AreEqual(expected, func());
        }

        private static MethodInfo GetOperatorMethodInfo<T>(string @operator, int argIndex)
        {
            var methodName = "op_" + @operator;
            var method =
                typeof (IndicatorBase<IndicatorDataPoint>).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .SingleOrDefault(x => x.Name == methodName && x.GetParameters()[argIndex].ParameterType == typeof(T));

            if (method == null)
            {
                Assert.Fail("Failed to find method for " + @operator + " of type " + typeof(T).Name + " at index: " + argIndex);
            }

            return method;
        }

        private class TestIndicator : Indicator
        {
            /// <summary>
            ///     Initializes a new instance of the Indicator class using the specified name.
            /// </summary>
            /// <param name="name">The name of this indicator</param>
            public TestIndicator(string name)
                : base(name)
            {
            }
            /// <summary>
            ///     Initializes a new instance of the Indicator class using the name "test"
            /// </summary>
            public TestIndicator()
                : base("test")
            {
            }

            /// <summary>
            ///     Gets a flag indicating when this indicator is ready and fully initialized
            /// </summary>
            public override bool IsReady
            {
                get { return true; }
            }

            /// <summary>
            ///     Computes the next value of this indicator from the given state
            /// </summary>
            /// <param name="input">The input given to the indicator</param>
            /// <returns>A new value for this indicator</returns>
            protected override decimal ComputeNextValue(IndicatorDataPoint input)
            {
                return input.Value;
            }
        }
    }
}