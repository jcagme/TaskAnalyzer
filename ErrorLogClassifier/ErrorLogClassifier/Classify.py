import jellyfish
import numpy as np
import pandas as pd
import pickle
import sys

from difflib import SequenceMatcher
from sklearn import svm, preprocessing

def similarity(x,y):
    return SequenceMatcher(None, content[x], content[y]).ratio()

def jaro_winkler(x,y):
    return jellyfish.jaro_winkler(content[x],content[y])

def damerau_levenshtein(x,y):
    return 10000 - jellyfish.damerau_levenshtein_distance(content[x],content[y])

def custom_distance(i,j):
    return (SequenceMatcher(None, i, j).ratio() * jellyfish.jaro_winkler(i,j) * 1000) * (10000 - jellyfish.damerau_levenshtein_distance(i,j))

representativeElementOfEqClass = pd.read_csv('representativeelements.csv')['Representative element of equivalence class aka cluster'].tolist()
pickleClf = open('linearClf.pickle','rb')
linearClf = pickle.load(pickleClf)

getEqClass = sys.argv[1]
vector = []
for r in representativeElementOfEqClass:
    vector.append(custom_distance(getEqClass, r))
vector = preprocessing.scale(np.array(vector))
print("Cluster no. " + str(linearClf.predict(vector.reshape(1,-1))[0]-1))
