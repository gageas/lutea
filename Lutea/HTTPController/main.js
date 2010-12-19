var _file_name = undefined;
var _playlistQuery = undefined;

var loadDataTimer = null;
var loadPlaylistTimer = null;
var commitQueryTimer = null;

var emphasized_element = null;

var Controller = {
	Play : function(){hitPath("./?mode=control&operation=play");},
	Stop : function(){hitPath("./?mode=control&operation=stop");},
	Next : function(){hitPath("./?mode=control&operation=next");},
	Prev : function(){hitPath("./?mode=control&operation=prev");},
	PlayPause : function(){hitPath("./?mode=control&operation=playpause");},
	Quit : function(){hitPath("./?mode=control&operation=quit");}
};

/*
 初期化
*/
function init() {
	loadData(false);
	loadPlaylist(false);
}

/*
 プレーヤの現在の状態を読み込む
*/
function loadData(useComet) {
    var ajax = new Ajax();
    ajax.open("GET", "./?mode=xml" + (useComet?"&comet=true":""));
    ajax.onload = function (xmlhttp) {
		var xml = xmlhttp.responseXML;
		if(xml == null)return;
        var file_name = getTagValue(xml,"file_name");
        if (file_name !== _file_name) {
            _file_name = file_name;
            if(file_name == null || file_name.match(/^\s+$/)){
	            $("infoView").innerHTML = "Stop";
            }else{
	            $("infoView").innerHTML = "<small>" + getTagValue(xml,"tagAlbum") + " <sup>#" + getTagValue(xml,"tagTracknumber") + "</sup></small><br>" + 
	            "<big>" + getTagValue(xml,"tagTitle") + " - " + getTagValue(xml,"tagArtist") + "</big>";
	            emphasize(file_name);
			}
			$("coverArt").src = "./?mode=cover&dummy=" + Math.random();
        }
        var playlistQuery = getTagValue(xml,"playlistQuery");
        if(playlistQuery !== _playlistQuery){
        	_playlistQuery = playlistQuery;
        	loadPlaylist();
        }
        if(loadDataTimer) clearTimeout(loadDataTimer);
        var now = new Date();
		var hours    = now.getHours();    // 時
		var minutes  = now.getMinutes();  // 分
		var seconds  = now.getSeconds();  // 秒
 
		if ( hours   < 10 ) hours   = '0' + hours;
		if ( minutes < 10 ) minutes = '0' + minutes;
		if ( seconds < 10 ) seconds = '0' + seconds;

        document.title = "Lutea WEB Interface " + hours + ":" + minutes + ":" + seconds + " sync OK.";
        loadDataTimer = setTimeout(function(){loadData(true);}, 100);
    }
    ajax.send(null);
}

/*
 playlistを読み込む
*/
function loadPlaylist(useComet){
    var ajax = new Ajax();
    ajax.open("GET", "./?mode=xml&type=playlist" + (useComet?"&comet=true":""));
    ajax.onload = function (xmlhttp) {
		var xml = xmlhttp.responseXML;
		if(xml == null)return;
		var items = xml.getElementsByTagName("item");

		var doc = getIframeDoc();
        doc.body.innerHTML = "";

        var ul = doc.createElement("ul");
        ul.className = "playlistItemWrap";
        for(var i=0;i<items.length;i++){
        	var item = items[i];
        	var li = doc.createElement("li");
        	li.className = "playlistItem";
        	li.innerText = li.textContent = item.getAttribute("tagAlbum") + " - " + item.getAttribute("tagTitle") + "/" + item.getAttribute("tagArtist");
        	li.id = escape(item.getAttribute("file_name"));
        	var index = item.getAttribute("index");
        	li.onclick = (function(i){
        		return function(){
		            $("infoView").innerHTML = "Loading...";
		            setTimeout(function(){hitPath("./?mode=control&operation=playitem&index=" + i);},1);      		
	        	}
        	})(i);
        	ul.appendChild(li);
            if(loadPlaylistTimer) clearTimeout(loadPlaylistTimer);
	        loadPlaylistTimer = setTimeout(function(){loadPlaylist(true);}, 100);
        }
        doc.body.appendChild(ul);
        emphasize(_file_name);
    }
    ajax.send(null);
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
	commitQueryTimer = setTimeout(function(){
		hitPath("./?mode=control&operation=createPlaylist&query=" + encodeURIComponent(escape(query)));
	}, 40);
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
	this.open = function(method,path){xmlhttp.open(method,path);};
	this.send = function(content){xmlhttp.send(content);};
	this.onload = function(){};
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