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

using NUnit.Framework;
using Python.Runtime;
using System;

namespace QuantConnect.Tests.Python
{
    [TestFixture, Category("TravisExclude")]
    public class PythonPackagesTests
    {
        [Test]
        public void NumpyTest()
        {
            AssetCode(
                @"
import numpy
def RunTest():
    return numpy.pi"
            );
        }

        [Test]
        public void ScipyTest()
        {
            AssetCode(
                @"
import scipy
import numpy
def RunTest():
    return scipy.mean(numpy.array([1, 2, 3, 4, 5]))"
            );
        }

        [Test]
        public void SklearnTest()
        {
            AssetCode(
                @"
from sklearn.ensemble import RandomForestClassifier
def RunTest():
    return RandomForestClassifier()"
            );
        }

        [Test]
        public void CvxoptTest()
        {
            AssetCode(
                @"
import cvxopt
def RunTest():
    return cvxopt.matrix([1.0, 2.0, 3.0, 4.0, 5.0, 6.0], (2,3))"
            );
        }

        [Test]
        public void TalibTest()
        {
            AssetCode(
                @"
import numpy
import talib
def RunTest():
    return talib.SMA(numpy.random.random(100))"
            );
        }

        [Test]
        public void BlazeTest()
        {
            AssetCode(
                @"
import blaze
def RunTest():
    accounts = blaze.symbol('accounts', 'var * {id: int, name: string, amount: int}')
    deadbeats = accounts[accounts.amount < 0].name
    L = [[1, 'Alice',   100],
         [2, 'Bob',    -200],
         [3, 'Charlie', 300],
         [4, 'Denis',   400],
         [5, 'Edith',  -500]]
    return blaze.compute(deadbeats, L)"
            );
        }

        [Test]
        public void CvxpyTest()
        {
            AssetCode(
                @"
import numpy
import cvxpy
def RunTest():
    numpy.random.seed(1)
    n = 10
    mu = numpy.abs(numpy.random.randn(n, 1))
    Sigma = numpy.random.randn(n, n)
    Sigma = Sigma.T.dot(Sigma)

    w = cvxpy.Variable(n)
    gamma = cvxpy.Parameter(nonneg=True)
    ret = mu.T*w
    risk = cvxpy.quad_form(w, Sigma)
    return cvxpy.Problem(cvxpy.Maximize(ret - gamma*risk), [cvxpy.sum(w) == 1, w >= 0])"
            );
        }

        [Test]
        public void StatsmodelsTest()
        {
            AssetCode(
                @"
import numpy
import statsmodels.api as sm
def RunTest():
    nsample = 100
    x = numpy.linspace(0, 10, 100)
    X = numpy.column_stack((x, x**2))
    beta = numpy.array([1, 0.1, 10])
    e = numpy.random.normal(size=nsample)

    X = sm.add_constant(X)
    y = numpy.dot(X, beta) + e

    model = sm.OLS(y, X)
    results = model.fit()
    return results.summary()"
            );
        }

        [Test]
        public void PykalmanTest()
        {
            AssetCode(
                @"
import numpy
from pykalman import KalmanFilter
def RunTest():
    kf = KalmanFilter(transition_matrices = [[1, 1], [0, 1]], observation_matrices = [[0.1, 0.5], [-0.3, 0.0]])
    measurements = numpy.asarray([[1,0], [0,0], [0,1]])  # 3 observations
    kf = kf.em(measurements, n_iter=5)
    return kf.filter(measurements)"
            );
        }

        [Test]
        public void CopulalibTest()
        {
            AssetCode(
                @"
import numpy
from copulalib.copulalib import Copula
def RunTest():
    x = numpy.random.normal(size=100)
    y = 2.5 * x + numpy.random.normal(size=100)

    #Make the instance of Copula class with x, y and clayton family::
    return Copula(x, y, family = 'clayton')"
            );
        }

        [Test]
        public void TheanoTest()
        {
            AssetCode(
                @"
import theano
def RunTest():
    a = theano.tensor.vector()          # declare variable
    out = a + a ** 10               # build symbolic expression
    f = theano.function([a], out)   # compile function
    return f([0, 1, 2])"
            );
        }

        [Test]
        public void XgboostTest()
        {
            AssetCode(
                @"
import numpy
import xgboost
def RunTest():
    data = numpy.random.rand(5,10)            # 5 entities, each contains 10 features
    label = numpy.random.randint(2, size=5)   # binary target
    return xgboost.DMatrix( data, label=label)"
            );
        }

        [Test]
        public void ArchTest()
        {
            AssetCode(
                @"
import numpy
from arch import arch_model
def RunTest():
    r = numpy.array([0.945532630498276,
        0.614772790142383,
        0.834417758890680,
        0.862344782601800,
        0.555858715401929,
        0.641058419842652,
        0.720118656981704,
        0.643948007732270,
        0.138790608092353,
        0.279264178231250,
        0.993836948076485,
        0.531967023876420,
        0.964455754192395,
        0.873171802181126,
        0.937828816793698])

    garch11 = arch_model(r, p=1, q=1)
    res = garch11.fit(update_freq=10)
    return res.summary()"
            );
        }

        [Test]
        public void KerasTest()
        {
            AssetCode(
                @"
import numpy
from keras.models import Sequential
from keras.layers import Dense, Activation
def RunTest():
    # Initialize the constructor
    model = Sequential()

    # Add an input layer
    model.add(Dense(12, activation='relu', input_shape=(11,)))

    # Add one hidden layer
    model.add(Dense(8, activation='relu'))

    # Add an output layer
    model.add(Dense(1, activation='sigmoid'))
    return model"
            );
        }

        [Test]
        public void TensorflowTest()
        {
            AssetCode(
                @"
import tensorflow as tf
def RunTest():
    node1 = tf.constant(3.0, tf.float32)
    node2 = tf.constant(4.0) # also tf.float32 implicitly
    sess = tf.Session()
    node3 = tf.add(node1, node2)
    return sess.run(node3)"
            );
        }

        [Test]
        public void DeapTest()
        {
            AssetCode(
                @"
import numpy
from deap import algorithms, base, creator, tools
def RunTest():
    # onemax example evolves to print list of ones: [1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
    numpy.random.seed(1)
    def evalOneMax(individual):
        return sum(individual),

    creator.create('FitnessMax', base.Fitness, weights=(1.0,))
    creator.create('Individual', list, typecode = 'b', fitness = creator.FitnessMax)

    toolbox = base.Toolbox()
    toolbox.register('attr_bool', numpy.random.randint, 0, 1)
    toolbox.register('individual', tools.initRepeat, creator.Individual, toolbox.attr_bool, 10)
    toolbox.register('population', tools.initRepeat, list, toolbox.individual)
    toolbox.register('evaluate', evalOneMax)
    toolbox.register('mate', tools.cxTwoPoint)
    toolbox.register('mutate', tools.mutFlipBit, indpb = 0.05)
    toolbox.register('select', tools.selTournament, tournsize = 3)

    pop = toolbox.population(n = 50)
    hof = tools.HallOfFame(1)
    stats = tools.Statistics(lambda ind: ind.fitness.values)
    stats.register('avg', numpy.mean)
    stats.register('std', numpy.std)
    stats.register('min', numpy.min)
    stats.register('max', numpy.max)

    pop, log = algorithms.eaSimple(pop, toolbox, cxpb = 0.5, mutpb = 0.2, ngen = 30,
                                   stats = stats, halloffame = hof, verbose = False) # change to verbose=True to see evolution table
    return hof[0]"
            );
        }

        [Test]
        public void QuantlibTest()
        {
            AssetCode(
                @"
import QuantLib as ql
def RunTest():
    todaysDate = ql.Date(15, 1, 2015)
    ql.Settings.instance().evaluationDate = todaysDate
    spotDates = [ql.Date(15, 1, 2015), ql.Date(15, 7, 2015), ql.Date(15, 1, 2016)]
    spotRates = [0.0, 0.005, 0.007]
    dayCount = ql.Thirty360()
    calendar = ql.UnitedStates()
    interpolation = ql.Linear()
    compounding = ql.Compounded
    compoundingFrequency = ql.Annual
    spotCurve = ql.ZeroCurve(spotDates, spotRates, dayCount, calendar, interpolation,
                             compounding, compoundingFrequency)
    return ql.YieldTermStructureHandle(spotCurve)"
            );
        }

        [Test]
        public void CopulaTest()
        {
            AssetCode(
                @"
from copulas.univariate.gaussian import GaussianUnivariate
import pandas as pd
def RunTest():
    data=pd.DataFrame({'feature_01': [5.1, 4.9, 4.7, 4.6, 5.0]})
    feature1 = data['feature_01']
    gu = GaussianUnivariate()
    gu.fit(feature1)
    return gu"
            );
        }

        [Test]
        public void HmmlearnTest()
        {
            AssetCode(
                @"
import numpy as np
from hmmlearn import hmm
def RunTest():
    # Build an HMM instance and set parameters
    model = hmm.GaussianHMM(n_components=4, covariance_type='full')
    
    # Instead of fitting it from the data, we directly set the estimated
    # parameters, the means and covariance of the components
    model.startprob_ = np.array([0.6, 0.3, 0.1, 0.0])
    # The transition matrix, note that there are no transitions possible
    # between component 1 and 3
    model.transmat_ = np.array([[0.7, 0.2, 0.0, 0.1],
                               [0.3, 0.5, 0.2, 0.0],
                               [0.0, 0.3, 0.5, 0.2],
                               [0.2, 0.0, 0.2, 0.6]])
    # The means of each component
    model.means_ = np.array([[0.0,  0.0],
                             [0.0, 11.0],
                             [9.0, 10.0],
                             [11.0, -1.0]])
    # The covariance of each component
    model.covars_ = .5 * np.tile(np.identity(2), (4, 1, 1))

    # Generate samples
    return model.sample(500)"
            );
        }

        [Test]
        public void PomegranateTest()
        {
            AssetCode(
                @"
from pomegranate import *
def RunTest():
    d1 = NormalDistribution(5, 2)
    d2 = LogNormalDistribution(1, 0.3)
    d3 = ExponentialDistribution(4)
    d = IndependentComponentsDistribution([d1, d2, d3])
    X = [6.2, 0.4, 0.9]
    return d.log_probability(X)"
            );
        }

        [Test]
        public void LightgbmTest()
        {
            AssetCode(
                @"
import lightgbm as lgb
import numpy as np
import pandas as pd
from scipy.special import expit
def RunTest():
    # Simulate some binary data with a single categorical and
    # single continuous predictor
    np.random.seed(0)
    N = 1000
    X = pd.DataFrame({
        'continuous': range(N),
        'categorical': np.repeat([0, 1, 2, 3, 4], N / 5)
    })
    CATEGORICAL_EFFECTS = [-1, -1, -2, -2, 2]
    LINEAR_TERM = np.array([
        -0.5 + 0.01 * X['continuous'][k]
        + CATEGORICAL_EFFECTS[X['categorical'][k]] for k in range(X.shape[0])
    ]) + np.random.normal(0, 1, X.shape[0])
    TRUE_PROB = expit(LINEAR_TERM)
    Y = np.random.binomial(1, TRUE_PROB, size=N)
    
    return {
        'X': X,
        'probability_labels': TRUE_PROB,
        'binary_labels': Y,
        'lgb_with_binary_labels': lgb.Dataset(X, Y),
        'lgb_with_probability_labels': lgb.Dataset(X, TRUE_PROB),
        }"
            );
        }

        [Test]
        public void FbProphetTest()
        {
            AssetCode(
                @"
import pandas as pd
from fbprophet import Prophet
def RunTest():
    df=pd.DataFrame({'ds': ['2007-12-10', '2007-12-11', '2007-12-12', '2007-12-13', '2007-12-14'], 'y': [9.590761, 8.519590, 8.183677, 8.072467, 7.893572]})
    m = Prophet()
    m.fit(df)
    future = m.make_future_dataframe(periods=365)
    return m.predict(future)"
            );
        }

        [Test]
        public void FastAiTest()
        {
            AssetCode(
                @"
from fastai.text import *
def RunTest():
    return 'Test is only importing the module, since available tests take too long'"
            );
        }

        [Test]
        public void PyramidArimaTest()
        {
            AssetCode(
                @"
import numpy as np
import pyramid as pm
from pyramid.datasets import load_wineind
def RunTest():
    # this is a dataset from R
    wineind = load_wineind().astype(np.float64)

    # fit stepwise auto-ARIMA
    stepwise_fit = pm.auto_arima(wineind, start_p=1, start_q=1,
                                 max_p=3, max_q=3, m=12,
                                 start_P=0, seasonal=True,
                                 d=1, D=1, trace=True,
                                 error_action='ignore',    # don't want to know if an order does not work
                                 suppress_warnings=True,   # don't want convergence warnings
                                 stepwise=True)            # set to stepwise
    
    return stepwise_fit.summary()"
            );
        }

        [Test]
        public void StableBaselinesTest()
        {
            AssetCode(
                @"
from stable_baselines.common.cmd_util import make_atari_env
from stable_baselines import PPO2
def RunTest():
    # There already exists an environment generator that will make and wrap atari environments correctly
    env = make_atari_env('DemonAttackNoFrameskip-v4', num_env=8, seed=0)

    model = PPO2('CnnPolicy', env)
    model.learn(total_timesteps=10)

    obs = env.reset()
    return model.predict(obs)"
            );
        }

        [Test]
        public void GensimTest()
        {
            AssetCode(
                @"
from gensim import models

def RunTest():
    # https://radimrehurek.com/gensim/tutorial.html
    corpus = [[(0, 1.0), (1, 1.0), (2, 1.0)],
              [(2, 1.0), (3, 1.0), (4, 1.0), (5, 1.0), (6, 1.0), (8, 1.0)],
              [(1, 1.0), (3, 1.0), (4, 1.0), (7, 1.0)],
              [(0, 1.0), (4, 2.0), (7, 1.0)],
              [(3, 1.0), (5, 1.0), (6, 1.0)],
              [(9, 1.0)],
              [(9, 1.0), (10, 1.0)],
              [(9, 1.0), (10, 1.0), (11, 1.0)],
              [(8, 1.0), (10, 1.0), (11, 1.0)]]

    tfidf = models.TfidfModel(corpus)
    vec = [(0, 1), (4, 1)]
    return f'{tfidf[vec]}'"
            );
        }

        [Test]
        public void ScikitMultiflowTest()
        {
            AssetCode(
                @"
from skmultiflow.data import WaveformGenerator
from skmultiflow.trees import HoeffdingTree
from skmultiflow.evaluation import EvaluatePrequential

def RunTest():
    # 1. Create a stream
    stream = WaveformGenerator()
    stream.prepare_for_use()

    # 2. Instantiate the HoeffdingTree classifier
    ht = HoeffdingTree()

    # 3. Setup the evaluator
    evaluator = EvaluatePrequential(show_plot=False,
                                    pretrain_size=200,
                                    max_samples=20000)

    # 4. Run evaluation
    evaluator.evaluate(stream=stream, model=ht)
    return 'Test passed, module exists'"
            );
        }

        [Test]
        public void ScikitOptimizeTest()
        {
            AssetCode(
                @"
import numpy as np
from skopt import gp_minimize

def f(x):
    return (np.sin(5 * x[0]) * (1 - np.tanh(x[0] ** 2)) * np.random.randn() * 0.1)

def RunTest():
    res = gp_minimize(f, [(-2.0, 2.0)])
    return f'Test passed: {res}'"
            );
        }

        [Test]
        public void CremeTest()
        {
            AssetCode(
                @"
from creme import datasets

def RunTest():
    X_y = datasets.Bikes()
    x, y = next(iter(X_y))
    return f'Number of bikes: {y}'"
            );
        }

        [Test]
        public void NltkTest()
        {
            AssetCode(
                @"
import nltk.data

def RunTest():
    text = '''
    Punkt knows that the periods in Mr. Smith and Johann S. Bach
    do not mark sentence boundaries.  And sometimes sentences
    can start with non-capitalized words.  i is a good variable
    name.
    '''
    sent_detector = nltk.data.load('tokenizers/punkt/english.pickle')
    return '\n-----\n'.join(sent_detector.tokenize(text.strip()))"
            );
        }

        public void NltkVaderTest()
        {
            AssetCode(
                @"
from nltk.sentiment.vader import SentimentIntensityAnalyzer
from nltk import tokenize

def RunTest():
    sentences = [
        'VADER is smart, handsome, and funny.', # positive sentence example...    'VADER is smart, handsome, and funny!', # punctuation emphasis handled correctly (sentiment intensity adjusted)
        'VADER is very smart, handsome, and funny.',  # booster words handled correctly (sentiment intensity adjusted)
        'VADER is VERY SMART, handsome, and FUNNY.',  # emphasis for ALLCAPS handled
        'VADER is VERY SMART, handsome, and FUNNY!!!',# combination of signals - VADER appropriately adjusts intensity
        'VADER is VERY SMART, really handsome, and INCREDIBLY FUNNY!!!',# booster words & punctuation make this close to ceiling for score
        'The book was good.',         # positive sentence
        'The book was kind of good.', # qualified positive sentence is handled correctly (intensity adjusted)
        'The plot was good, but the characters are uncompelling and the dialog is not great.', # mixed negation sentence
        'A really bad, horrible book.',       # negative sentence with booster words
        'At least it isn't a horrible book.', # negated negative sentence with contraction
        ':) and :D',     # emoticons handled
        '',              # an empty string is correctly handled
        'Today sux',     #  negative slang handled
        'Today sux!',    #  negative slang with punctuation emphasis handled
        'Today SUX!',    #  negative slang with capitalization emphasis
        'Today kinda sux! But I'll get by, lol' # mixed sentiment example with slang and constrastive conjunction 'but'
    ]
    paragraph = 'It was one of the worst movies I've seen, despite good reviews. \
        Unbelievably bad acting!! Poor direction.VERY poor production. \
        The movie was bad.Very bad movie.VERY bad movie.VERY BAD movie.VERY BAD movie!'

    lines_list = tokenize.sent_tokenize(paragraph)
    sentences.extend(lines_list)

    sid = SentimentIntensityAnalyzer()
    for sentence in sentences:
        ss = sid.polarity_scores(sentence)

    return f'{sid}'"
            );
        }

        [Test]
        public void MlfinlabTest()
        {
            AssetCode(
                @"
from mlfinlab.portfolio_optimization.hrp import HierarchicalRiskParity
from mlfinlab.portfolio_optimization.mean_variance import MeanVarianceOptimisation
import numpy as np
import pandas as pd
import os

def RunTest():
# Read in data
    data_file = os.getcwd() + '/TestData/stock_prices.csv'
    stock_prices = pd.read_csv(data_file, parse_dates=True, index_col='Date') # The date column may be named differently for your input.

    # Compute HRP weights
    hrp = HierarchicalRiskParity()
    hrp.allocate(asset_prices=stock_prices, resample_by='B')
    hrp_weights = hrp.weights.sort_values(by=0, ascending=False, axis=1)

    # Compute IVP weights
    mvo = MeanVarianceOptimisation()
    mvo.allocate(asset_prices=stock_prices, solution='inverse_variance', resample_by='B')
    ivp_weights = mvo.weights.sort_values(by=0, ascending=False, axis=1)
    
    return f'HRP: {hrp_weights} IVP: {ivp_weights}'"
            );
        }

        [Test]
        public void JaxTest()
        {
            AssetCode(
                @"
import jax.numpy as np
from jax import grad, jit, vmap

def predict(params, inputs):
  for W, b in params:
    outputs = np.dot(inputs, W) + b
    inputs = np.tanh(outputs)
  return outputs

def logprob_fun(params, inputs, targets):
  preds = predict(params, inputs)
  return np.sum((preds - targets)**2)

def RunTest():
    grad_fun = jit(grad(logprob_fun))  # compiled gradient evaluation function
    return jit(vmap(grad_fun, in_axes=(None, 0, 0)))  # fast per-example grads"
            );
        }

        [Test]
        public void NeuralTangentsTest()
        {
            AssetCode(
                @"
from jax import random
import neural_tangents as nt
from neural_tangents import stax

def RunTest():
    init_fn, apply_fn, kernel_fn = stax.serial(
        stax.Dense(512), stax.Relu(),
        stax.Dense(512), stax.Relu(),
        stax.Dense(1)
    )

    key1, key2 = random.split(random.PRNGKey(1))
    x1 = random.normal(key1, (10, 100))
    x2 = random.normal(key2, (20, 100))

    x_train, x_test = x1, x2
    y_train = random.uniform(key1, shape=(10, 1))  # training targets

    y_test_nngp = nt.predict.gp_inference(kernel_fn, x_train, y_train, x_test, get='nngp')

    # (20, 1) np.ndarray test predictions of an infinite Bayesian network
    return nt.predict.gp_inference(kernel_fn, x_train, y_train, x_test, get='ntk')"
            );
        }


        [Test]
        public void SmmTest()
        {
            AssetCode(
                @"
import ssm

def RunTest():
    T = 100  # number of time bins
    K = 5    # number of discrete states
    D = 2    # dimension of the observations

    # make an hmm and sample from it
    hmm = ssm.HMM(K, D, observations='gaussian')
    z, y = hmm.sample(T)
    test_hmm = ssm.HMM(K, D, observations='gaussian')
    test_hmm.fit(y)
    return test_hmm.most_likely_states(y)"
            );
        }

        [Test]
        public void RiskparityportfolioTest()
        {
            AssetCode(
                @"
import riskparityportfolio as rp
import numpy as np

def RunTest():
    Sigma = np.vstack((np.array((1.0000, 0.0015, -0.0119)),
                   np.array((0.0015, 1.0000, -0.0308)),
                   np.array((-0.0119, -0.0308, 1.0000))))
    b = np.array((0.1594, 0.0126, 0.8280))
    w = rp.vanilla.design(Sigma, b)
    rc = w @ (Sigma * w)
    return rc/np.sum(rc)"
            );
        }

        [Test]
        public void PyrbTest()
        {
            AssetCode(
                @"
import pandas as pd
import numpy as np
from pyrb import ConstrainedRiskBudgeting

def RunTest():
    vol = [0.05,0.05,0.07,0.1,0.15,0.15,0.15,0.18]
    cor = np.array([[100,  80,  60, -20, -10, -20, -20, -20],
               [ 80, 100,  40, -20, -20, -10, -20, -20],
               [ 60,  40, 100,  50,  30,  20,  20,  30],
               [-20, -20,  50, 100,  60,  60,  50,  60],
               [-10, -20,  30,  60, 100,  90,  70,  70],
               [-20, -10,  20,  60,  90, 100,  60,  70],
               [-20, -20,  20,  50,  70,  60, 100,  70],
               [-20, -20,  30,  60,  70,  70,  70, 100]])/100
    cov = np.outer(vol,vol)*cor
    C = None
    d = None

    CRB = ConstrainedRiskBudgeting(cov,C=C,d=d)
    CRB.solve()
    return CRB"
            );
        }

        [Test]
        public void CopulaeTest()
        {
            AssetCode(
                @"
from copulae import NormalCopula
import numpy as np

def RunTest():
    np.random.seed(8)
    data = np.random.normal(size=(300, 8))
    cop = NormalCopula(8)
    cop.fit(data)

    cop.random(10)  # simulate random number

    # getting parameters
    p = cop.params
    # cop.params = ...  # you can override parameters too, even after it's fitted!  

    # get a summary of the copula. If it's fitted, fit details will be present too
    return cop.summary()"
            );
        }
        [Test]
        public void SanityClrInstallation()
        {
            AssetCode(
                @"
from os import walk
import setuptools as _

def RunTest():
    try:
        import clr
        clr.AddReference()
        print('No clr errors')
        #Checks complete
    except: #isolate error cause
        try:
            import clr
            print('clr exists') #Module exists
            try:
                f = []
                for (dirpath, dirnames, filenames) in walk(print(clr.__path__)):
                    f.extend(filenames)
                    break
                return(f.values['style_builder.py']) #If this is reached, likely due to an issue with this file itself
            except:
                print('no style_builder') #pythonnet install error, most likely

        except:
            print('clr does not exist') #Only remaining cause"
            );
        }

        /// <summary>
        /// Simple test for modules that don't have short test example
        /// </summary>
        /// <param name="module">The module we are testing</param>
        /// <param name="version">The module version</param>
        [TestCase("pulp", "1.6.8", "VERSION")]
        [TestCase("pymc3", "3.8", "__version__")]
        [TestCase("pypfopt", "pypfopt", "__name__")]
        [TestCase("wrapt", "1.12.1", "__version__")]
        [TestCase("tslearn", "0.3.1", "__version__")]
        [TestCase("tweepy", "3.8.0", "__version__")]
        [TestCase("pywt", "1.1.1", "__version__")]
        [TestCase("umap", "0.4.1", "__version__")]
        [TestCase("dtw", "1.0.5", "__version__")]
        [TestCase("mplfinance", "0.12.3a3", "__version__")]
        [TestCase("cufflinks", "0.17.3", "__version__")]
        [TestCase("ipywidgets", "7.5.1", "__version__")]
        [TestCase("astropy", "4.0.1.post1", "__version__")]
        [TestCase("gluonts", "0.4.3", "__version__")]
        [TestCase("gplearn", "0.4.1", "__version__")]
        [TestCase("h2o", "3.30.0.1", "__version__")]
        [TestCase("cntk", "2.7", "__version__")]
        [TestCase("featuretools", "0.13.4", "__version__")]
        [TestCase("pennylane", "0.8.1", "version()")]
        public void ModuleVersionTest(string module, string value, string attribute)
        {
            AssetCode(
                $@"
import {module}

def RunTest():
    assert({module}.{attribute} == '{value}')
    return 'Test passed, module exists'"
            );
        }

        private static void AssetCode(string code)
        {
            using (Py.GIL())
            {
                dynamic module = PythonEngine.ModuleFromString(Guid.NewGuid().ToString(), code);
                Assert.DoesNotThrow(() => module.RunTest());
            }
        }
    }
}
