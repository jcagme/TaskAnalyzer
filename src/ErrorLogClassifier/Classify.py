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

            categoryAndClass = json.dumps(data["clusters"][int(self.linearClf.predict(vector.reshape(1,-1))[0])])
            categoryAndClass = json.loads(categoryAndClass)
            logReports.append(LogReport(original, categoryAndClass["category"], categoryAndClass["class"]))

        return json.dumps([LogReport.returnJson(logReport) for logReport in logReports])

    def __init__(self):
        self.keywords = pd.read_csv("keywords.csv")['Keyword'].tolist()
        pickleClf = open('linearClf.pickle','rb')
        self.linearClf = pickle.load(pickleClf)
