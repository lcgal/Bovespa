from yahooquery import Ticker
from datetime import datetime
from pandas import DataFrame
import pandas as pd 
from pathlib import Path

def writeHistoricData(tickerName):
    ticker = Ticker(tickerName + ".SA")
    print(tickerName)
    data = ticker.history(start=startDate, end=endDate)
    if type(data) == a2:
        data = data.reset_index(level=0 , drop=True)
        data = data[data.volume > 1000]
        
        fileName = "C:\Repositorios\Bovespa\Historic data\\" + tickerName + ".csv"
    
        data.to_csv(Path(fileName),index = True,columns = ['open','close','low','high','volume'])
    



startDate = datetime(2000,1,1)
endDate = datetime(2021,2,12)


ticker2 = Ticker('VALE3.SA')
data2 = ticker2.history(start=startDate, end=endDate)
a2 = type(data2)

tickersDf = pd.read_excel(r'C:\Repositorios\Bovespa\ticks.xlsx')

for ind in tickersDf.index:
    
    tickerName = tickersDf['nome'][ind]
    writeHistoricData(tickerName)
    

