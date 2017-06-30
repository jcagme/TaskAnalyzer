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
        vector = []
        for r in self.representativeElementOfEqClass:
            vector.append(self.__custom_distance(log, r))
        vector = preprocessing.scale(np.array(vector))
        
        with open('clusters.json') as f:
            data = json.load(f)

        return json.dumps(data["clusters"][int(self.linearClf.predict(vector.reshape(1,-1))[0]-1)])

    def __init__(self):
        self.representativeElementOfEqClass = pd.read_csv('representativeelements.csv')['Representative element of equivalence class aka cluster'].tolist()
        pickleClf = open('linearClf.pickle','rb')
        self.linearClf = pickle.load(pickleClf)
