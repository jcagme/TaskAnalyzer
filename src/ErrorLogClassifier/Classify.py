import json
import numpy as np
import pandas as pd
import pickle
import sys

from LogReport import LogReport
from sklearn import svm, preprocessing

class Classify:

    def classifyLogs(self, logs):
        with open('clusters.json') as f:
            data = json.load(f)
        logReports = []

        for log in logs:
            original = log
            log = log.lower()
            vector = [0] * (len(self.keywords))
            for word in range(0, len(self.keywords)):
                if self.keywords[word] in log:
                    vector[word] = vector[word] + 1
            vector = np.array(vector)

            vectorToList = self.linearClf.predict_proba(vector.reshape(1,-1))[0].tolist()
            K = []
            for i, v in enumerate(vectorToList):
                K.append((v,i))
            K = sorted(K, reverse=True)
            categoriesThatMeetThreshold = []
            for k in K:
                if k[0] >= self.threshold:
                    categoriesThatMeetThreshold.append(k[1])
            
            categoriesThatMeetThreshold = categoriesThatMeetThreshold[:3]
            logReports.append(LogReport(original, [json.loads(json.dumps(data["clusters"][categoriesThatMeetThreshold[i]])) for i in range(0, len(categoriesThatMeetThreshold))]))

        return json.dumps([LogReport.returnJson(logReport) for logReport in logReports])

    def __init__(self):
        self.threshold = 0.65
        self.keywords = pd.read_csv("keywords.csv")['Keyword'].tolist()
        pickleClf = open('linearClf.pickle','rb')
        self.linearClf = pickle.load(pickleClf)
