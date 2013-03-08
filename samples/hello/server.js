var owin = require('../../lib/owin-connect.js')
    , express = require('express');

var app = express();
app.use(express.bodyParser());
app.all('/net', owin('Owin.Samples.dll'))
app.all('/node', function (req, res) {
    res.send(200, 'Hello from JavaScript! Time on server ' + new Date());
});

app.listen(3000);
