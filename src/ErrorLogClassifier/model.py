import os
import pickle
import sys

sys.path.append(os.path.abspath(os.path.join(os.path.dirname( __file__ ), 'virtualenv/Lib/site-packages')))

import numpy as np
import pandas as pd

from sklearn import svm, preprocessing, cross_validation
from sklearn.svm import SVR

data_vectors = []
target_vector = []

data = pd.read_csv("data.csv")
target = pd.read_csv("target.csv")
for i in range(0, len(data.columns)-1):
    data_vectors.append(data[str(i)].tolist())
    target_vector.append(target[str(i)].tolist())
target_vector = np.array([i for sublist in target_vector for i in sublist])    
data_vectors = np.array(data_vectors)

X = data_vectors
y = target_vector
X_train, X_test, y_train, y_test = cross_validation.train_test_split(X, y, test_size=0.25)

linearClf = svm.SVC(kernel='linear')
linearClf.fit(X_train, y_train)
linear_confidence = linearClf.score(X_test, y_test)
print("Linear confidence: " + str(linear_confidence))

with open('linearClf.pickle','wb') as f:
    pickle.dump(linearClf, f)