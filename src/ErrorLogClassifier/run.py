import json
import os
import sys
sys.path.append(os.path.abspath(os.path.join(os.path.dirname( __file__ ), 'virtualenv/Lib/site-packages')))

from Classify import Classify

classify = Classify()
postreqdata = json.loads(open(os.environ['req']).read())

response = open(os.environ['res'], 'w')
response.write(classify.classifyLogs(postreqdata))
response.close()