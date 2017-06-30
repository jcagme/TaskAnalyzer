import os
import json
import sys
sys.path.append(os.path.abspath(os.path.join(os.path.dirname( __file__ ), 'virtualenv/Lib/site-packages')))

from Classify import Classify

classify = Classify()
postreqdata = json.loads(open(os.environ['req']).read()) 
log = postreqdata['log']

response = open(os.environ['res'], 'w')
response.write(classify.classifyLog(log))
response.close()