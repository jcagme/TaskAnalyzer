import sys
import os
sys.path.append(os.path.abspath(os.path.join(os.path.dirname( __file__ ), 'virtualenv/Lib/site-packages')))
import pandas as pd

data = pd.read_csv(sys.argv[1])

# Generate keywords.csv
keywords = data['Keywords'].tolist()
_k = []
for k in keywords:
    x = k.split(',')
    for _x in x:
        _x = _x.strip()
        _k.append(_x)
keywords = sorted(set(list(_k)))

ID = []
Keyword = []
for i, x in enumerate(keywords):
    Keyword.append(x)
    ID.append(i)

df = pd.DataFrame({'Keyword':Keyword, 'ID':ID})
df.to_csv("keywords.csv")

# Generate target.csv
categories = data['Category'].tolist()
categories = sorted(list(set(categories)))
categories = dict((categories[i], i) for i in range(0, len(categories)))

categoryIndexes = []
DataCategories = data['Category'].tolist()
for i in range(0, len(DataCategories)):
    categoryIndexes.append(categories[DataCategories[i]])

df = pd.DataFrame({"":categoryIndexes})
df = df.transpose()
df.to_csv("target.csv")

# Generate data.csv
ErrorLogs = data['ErrorLog'].tolist()
keywordOccurrences = []
for error in ErrorLogs:
    vector = [0] * len(keywords)
    for i in range (0, len(keywords)):
        vector[i] = error.count(keywords[i])
    keywordOccurrences.append(vector)

df = pd.DataFrame(keywordOccurrences)
df = df.transpose()
df.to_csv("data.csv")