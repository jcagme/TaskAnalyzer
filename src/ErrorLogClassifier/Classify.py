import jellyfish
import json
import numpy as np
import pandas as pd
import pickle
import sys

from pprint import pprint
from difflib import SequenceMatcher
from sklearn import svm, preprocessing

class Classify:

    def __custom_distance(self, i,j):
        return (SequenceMatcher(None, i, j).ratio() * jellyfish.jaro_winkler(unicode(i),unicode(j)) * 1000) * (10000 - jellyfish.damerau_levenshtein_distance(unicode(i),unicode(j)))

    def classifyLog(self, log):
        log = log.lower()
        vector = [0] * (len(self.keywords))
        for word in range(0, len(self.keywords)):
            if self.keywords[word] in log:
                vector[word] = vector[word] + 1
        vector = np.array(vector)
        return self.linearClf.predict(vector.reshape(1,-1))[0]

    def __init__(self):
        self.keywords = pd.read_csv("keywords.csv")['Keyword'].tolist()
        pickleClf = open('linearClf.pickle','rb')
        self.linearClf = pickle.load(pickleClf)
