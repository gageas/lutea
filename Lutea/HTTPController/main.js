var _file_name = null;
var _playlistQuery = null;

var loadData_id = 0;
var loadPlaylist_id = 0;

var delayedPlaylistBuilder = null;
var delayedPlaylistBuilderPos = 0;

var commitQueryTimer = null;

var loadDataAjax = null;
var loadPlaylistAjax = null;

var emphasized_element = null;

var Controller = {
    Play: function () { hitPath("./?mode=control&operation=play"); },
    Stop: function () { hitPath("./?mode=control&operation=stop"); },
    Next: function () { hitPath("./?mode=control&operation=next"); },
    Prev: function () { hitPath("./?mode=control&operation=prev"); },
    PlayPause: function () { hitPath("./?mode=control&operation=playpause"); },
    VolUp: function () { hitPath("./?mode=control&operation=volup"); },
    VolDown: function () { hitPath("./?mode=control&operation=voldown"); },
    Quit: function () { hitPath("./?mode=control&operation=quit"); },
    PlayPlaylistItem: function (id) { hitPath("./?mode=control&operation=playitem&index=" + id); }
};

/*
 初期化
*/
function init() {
	loadData();
	loadPlaylist();
}

/*
 プレーヤの現在の状態を読み込む
*/
function loadData() {
    if (loadDataAjax) loadDataAjax.abort();
    loadDataAjax = new Ajax();

    // 20秒後にタイムアウト
    var timeoutTimer = setTimeout(function(){
    	loadDataAjax.abort();
        loadData();
    },20*1000);
    
    loadDataAjax.open("GET", "./?mode=xml&comet_id=" + loadData_id);
    loadDataAjax.onload = function (xmlhttp) {
        // タイムアウトタイマ解除
        clearTimeout(timeoutTimer);

        var now = new Date();
        var hours = now.getHours();    // 時
        var minutes = now.getMinutes();  // 分
        var seconds = now.getSeconds();  // 秒
        if (hours < 10) hours = '0' + hours;
        if (minutes < 10) minutes = '0' + minutes;
        if (seconds < 10) seconds = '0' + seconds;
        document.title = "Lutea WEB Interface " + hours + ":" + minutes + ":" + seconds + " sync OK.";

        var xml = xmlhttp.responseXML;
        if (xml == null) return;
        var file_name = getTagValue(xml, "file_name");
        if (file_name !== _file_name) {
            _file_name = file_name;
            setInfoView(file_name, xml);
        }
        var playlistQuery = getTagValue(xml, "playlistQuery");
        if (playlistQuery !== _playlistQuery) {
            _playlistQuery = playlistQuery;
            loadPlaylist();
        }
        loadData_id = getTagValue(xml, "comet_id");
        setTimeout(function () { loadData(); }, 50);
    }
    loadDataAjax.send(null);
}

function setInfoView(file_name, xml) {
    $("infoView").innerHTML = "";

    if (file_name == null || file_name.match(/^\s+$/)) {
        $("infoView").innerHTML = "Stop";
    } else {
        env.fields.album = getTagValue(xml, "tagAlbum");
        env.fields.tracknumber = getTagValue(xml, "tagTracknumber");
        env.fields.title = getTagValue(xml, "tagTitle");
        env.fields.artist = getTagValue(xml, "tagArtist");

        $("infoView").innerHTML = TitleFormatting(env, infoViewTF);

        emphasize(file_name);
    }
    $("coverArt").src = "./?mode=cover&dummy=" + Math.random();
}

/*
 playlistを読み込む
*/
function loadPlaylist() {
    if (loadPlaylistAjax) loadPlaylistAjax.abort();
    loadPlaylistAjax = new Ajax();
    
    // 20秒後にタイムアウト
    var timeoutTimer = setTimeout(function(){
    	loadPlaylistAjax.abort();
    	loadPlaylist();
    },20*1000);
    
    loadPlaylistAjax.open("GET", "./?mode=xml&type=playlist&comet_id=" + loadPlaylist_id);
    loadPlaylistAjax.onload = function (xmlhttp) {
        // タイムアウトタイマ解除
        clearTimeout(timeoutTimer);
        $('queryInput').style.backgroundColor = "";

        var xml = xmlhttp.responseXML;
        if (xml == null) return;
        var items = xml.getElementsByTagName("item");

        var doc = getIframeDoc();
        doc.body.innerHTML = "";

        delayedPlaylistBuilderPos = 0;
        if (delayedPlaylistBuilder) clearInterval(delayedPlaylistBuilder);

        delayedPlaylistBuilder = setInterval(function () { AppendPlaylistItem(doc, items); }, 100);
        AppendPlaylistItem(doc, items);

        loadPlaylist_id = getTagValue(xml, "comet_id");
        setTimeout(function () { loadPlaylist(); }, 100);
    }
    loadPlaylistAjax.send(null);
}

function AppendPlaylistItem(doc, items) {
    var N = 500;

    var ul = doc.createElement("ul");
    ul.className = "playlistItemWrap";

    for (var c = 0; c < N; c++) {
        var i = delayedPlaylistBuilderPos + c;
        if (i >= items.length) {
            setTimeout(function () {
                emphasize(_file_name);
                clearInterval(delayedPlaylistBuilder);
                delayedPlaylistBuilder = null;
            }, 0);
            break;
        }
        var item = items[i];
        var li = doc.createElement("li");
        li.className = "playlistItem";
        var _env = { func: env.func, fields: {
            "album": item.getAttribute("tagAlbum"),
            "title": item.getAttribute("tagTitle"),
            "artist": item.getAttribute("tagArtist")
        } };
        li.innerHTML = TitleFormatting(_env, playlistItemTF).toString();
        li.id = escape(item.getAttribute("file_name"));
        var index = item.getAttribute("index");
        li.onclick = (function (i) {
            return function () {
                onClickPlaylistItem(i);
            }
        })(i);
        ul.appendChild(li);
    }
    doc.body.appendChild(ul);
    delayedPlaylistBuilderPos += N;
}

function onClickPlaylistItem(i) {
    $("infoView").innerHTML = "Loading...";
    loadData_id = 0;
    _file_name = null;
    setTimeout(function () { Controller.PlayPlaylistItem(i); }, 1);
}

function showLargeCover() {
    $('coverOverlay').style.opacity = 0;
    $('coverOverlay').style.display = 'block';
    $('coverOverlay_img').style.visibility = 'hidden';
    $('coverOverlay_img').style.width = '';
    $('coverOverlay_img').src = "./?mode=coverorigsize&dummy=" + Math.random();
    var N = 10;
    for (var i = 0; i < N; i++) {
        setTimeout((function (i) { return function () { $('coverOverlay').style.opacity = i / N; } })(i), i * 20);
    }
    setTimeout(function () {
        $('coverOverlay').style.opacity = 1;
        if ($('coverOverlay_img').width > document.body.clientWidth) {
            $('coverOverlay_img').style.width = document.body.clientWidth;
        }
        $('coverOverlay_img').style.visibility = 'visible';
    }, N * 20);
}

function getIframeDoc(){
    var ifr = document.getElementById("playlistViewIframe");
    return ifr.contentDocument || ifr.contentWindow.document;
}

function emphasize(file_name){
	if(emphasized_element){
		emphasized_element.style.fontWeight = "";
		emphasized_element.style.backgroundColor = "";
	}
	emphasized_element = getIframeDoc().getElementById(escape(file_name));
	if(emphasized_element){
		emphasized_element.style.backgroundColor = "gold";
		emphasized_element.style.fontWeight = "bold";
	}
}

/*
 createPlaylistを実行する
*/
function commitQuery(query){
	if(query == "") return; // ""のときはクエリ投げない
	if(commitQueryTimer) clearTimeout(commitQueryTimer);
	commitQueryTimer = setTimeout(function () {
	    $('queryInput').style.backgroundColor = "#E6E682";
	    location.hash = encodeURIComponent(query);
	    hitPath("./?mode=control&operation=createPlaylist&query=" + encodeURIComponent(escape(query)));
	}, 20);
}

/*
 指定したURLをGETしにいくだけ。API叩き用
*/
function hitPath(path){
	var ajax = new Ajax();
	ajax.open("GET", path);
	ajax.send(null);
}

/*
　指定したDOMツリー中から名前がnameである要素を検索し、先頭のものの内容(テキスト)を返す
*/
function getTagValue(doc, name){
	var elements = doc.getElementsByTagName(name);
	if(elements.length > 0){
		try{
			return elements[0].firstChild.nodeValue;
		}catch(e){
		}
	}
	return null;
}

function Ajax(){
	var xmlhttp = new XMLHttpRequest();
	var self = this;
	var onload_fired = false;
	this.abort = function () { if(xmlhttp)xmlhttp.abort(); xmlhttp = null; };
	this.open = function (method, path) { xmlhttp.open(method, path); };
	this.send = function (content) { xmlhttp.send(content); };
	this.onload = function () { };
	xmlhttp.onreadystatechange = function(){
		if(xmlhttp.readyState == 4 && xmlhttp.status == 200){
			if(onload_fired)return;
			onload_fired = true;
			self.onload(xmlhttp);
		}
	}
}

function $(id) {
    return document.getElementById(id);
}

/*

Usage:
1) Evaluate source code directly
e.g. TitleFormatting(environment, "aaa%title%bbb");

2) Parse source code, then execute object code
e.g. var ocode = TitleFormatting.Prepare("aaa%title%bbb");
var result = TitleFormatting(environment, ocode);
	
Environment object:
Environment object provides functions($~~) and fields(%~~%).
e.g. var env = {func:{"if":TFIf,"if2":TFIf2}, fields:{title:"TITLE"}};
	
TitleFormatting Function:
TitleFormatting Function receives environment and list of arguments. Arguments are code object fragment.
TF Function should return TF Result object.
e.g. function TFGreater(env, args){
var x = args[0];
var y = args[1];
return new TitleFormatting.Result("", (TitleFormatting.Eval(env,x) > TitleFormatting.Eval(env,y)));
}
	
TitleFormatting Result Object:
TitleFormatting Result Object is combination of string and boolean.
Boolean flag indicates field(%~~%) expansion successed or not.
[ ] operator returns "" if flag is false.

*/
var TitleFormatting = (function () {
    // Title formatting Result object
    var TFResult = function (str, success) {
        this.str = str;
        this.success = success;
    }
    TFResult.prototype.toString = function () { return this.str; };

    // Title formatting Token objects
    // TF Function eg. $if
    var kTFFunc = function (name) {
        this.name = name;
    }
    kTFFunc.prototype.toString = function () { return "#func " + this.name; };

    // TF Field eg. %isPlaying%
    var kTFField = function (key) {
        this.key = key;
    }
    kTFField.prototype.toString = function () { return "%" + this.key + "%"; };

    var kTFComma = new Object();
    kTFComma.toString = function () { return "#,"; };

    var kTFOpen = new Object();
    kTFOpen.toString = function () { return "#("; };

    var kTFClose = new Object();
    kTFClose.toString = function () { return "#)"; };

    var kTFAnyOpen = new Object();
    kTFAnyOpen.toString = function () { return "#["; };

    var kTFAnyClose = new Object();
    kTFAnyClose.toString = function () { return "#]"; };

    var tokenize = function (src) {
        var tokens = [];
        var m = null;
        while (src.length > 0) {
            if (m = src.match(/^\$[a-z0-9]+/i)) {
                tokens.push(new kTFFunc(m[0].substr(1)));
                src = src.replace(m[0], "");
            } else if (m = src.match(/^\%([a-z_ ]+)\%/i)) {
                tokens.push(new kTFField(m[1]));
                src = src.replace(m[0], "");
            } else if (src.match(/^''/)) {
                tokens.push("'");
                src = src.substr(2);
            } else if (m = src.match(/^'([^']+)'/)) {
                tokens.push(m[1]);
                src = src.replace(m[0], "");
            } else if (src.match(/^\(/)) {
                tokens.push(kTFOpen);
                src = src.substr(1);
            } else if (src.match(/^\)/)) {
                tokens.push(kTFClose);
                src = src.substr(1);
            } else if (src.match(/^,/)) {
                tokens.push(kTFComma);
                src = src.substr(1);
            } else if (src.match(/^\[/)) {
                tokens.push(kTFAnyOpen);
                src = src.substr(1);
            } else if (src.match(/^\]/)) {
                tokens.push(kTFAnyClose);
                src = src.substr(1);
            } else {
                tokens.push(src.charAt(0));
                src = src.substr(1);
            }
        }
        return tokens;
    }

    var pushTextToken = function (current, token) {
        if (current.length == 0) {
            current.push(token);
        } else {
            if (typeof (current[current.length - 1]) == "string" || current[current.length - 1] instanceof String) {
                current[current.length - 1] += token;
            } else {
                if (current[current.length - 1] == null) current.pop();
                current.push(token);
            }
        }
    }
    var makeTree = function (tokens) {
        var tree = [];
        tree.parent = null;
        var current = tree;
        for (var i = 0, l = tokens.length; i < l; i++) {
            var token = tokens[i];
            if (token instanceof kTFFunc) {
                var block = [];
                block.func = token;
                block.parent = current;
                if (current[current.length - 1] == null) current.pop();
                current.push(block);
                current = block;
            } else if (token instanceof kTFField) {
                current.push(token);
            } else if (token == kTFOpen) {
                var block = [];
                block.parent = current;
                current.hasArgs = true;
                current.push(block);
                current = block;
            } else if (token == kTFClose) {
                current = current.parent;
                current = current.parent;
            } else if (token == kTFComma) {
                if (current.parent && current.parent.func) {
                    current = current.parent;
                    var block = [];
                    block.parent = current;
                    current.push(block);
                    current = block;
                } else {
                    pushTextToken(current, ",");
                }
            } else if (token == kTFAnyOpen) {
                var block = [];
                block.any = true;
                block.parent = current;
                if (current[current.length - 1] == null) current.pop();
                current.push(block);
                current = block;
            } else if (token == kTFAnyClose) {
                current = current.parent;
            } else {
                pushTextToken(current, token);
            }
        }
        return tree;
    }

    function evalTF(env, codeFragment) {
        var result = [];
        var success = false;

        // null
        if (codeFragment == null) return new TFResult("", false);

        // literal
        if (typeof (codeFragment) == "string" || codeFragment instanceof String) {
            return new TFResult(codeFragment, false);
        }

        // result object
        if (codeFragment instanceof TFResult) {
            return codeFragment;
        }

        // field
        if (codeFragment instanceof kTFField) {
            var val = env.fields[codeFragment.key];
            if (val) {
                return new TFResult(val, true);
            } else {
                return new TFResult("?", false);
            }
        }

        // codeFragment list
        if (codeFragment.func) { // list of arguments (function call)
            if (!codeFragment.hasArgs) return new TFResult("", false);
            var name = codeFragment.func.name;
            var func = env.func[name];
            if (func) {
                var res = func(env, codeFragment);
                success = success || res.success;
                result.push(res);
            } else {
                if (env.function_missing && env.function_missing instanceof Function) {
                    var res = env.function_missing(name, env, codeFragment);
                    success = success || res.success;
                    result.push(res);
                } else {
                    result.push(new TFResult("[UNKNOWN FUNCTION " + name + "]", false));
                }
            }
        } else {
            for (var i = 0, l = codeFragment.length; i < l; i++) {
                var res = evalTF(env, codeFragment[i]);
                success = success || res.success;
                result.push(res);
            }
        }
        if (codeFragment.any && !success) return new TFResult("", false);
        return new TFResult(result.join(""), success);
    }

    // closure object
    var c = function (env, code) {
        if (code instanceof Array) {
            // if code is object code
            return evalTF(env, code);
        } else {
            // if code is source code
            return evalTF(env, makeTree(tokenize(code)));
        }
    }

    c.Eval = evalTF;
    c.Result = TFResult;
    c.Prepare = function (code) { return makeTree(tokenize(code)); };

    return c;
})();