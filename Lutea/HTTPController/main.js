var _file_name = undefined;
var _playlistQuery = undefined;
var loadDataTimer = null;
var Controller = {
	Play : function(){hitPath("./?mode=control&operation=play");},
	Stop : function(){hitPath("./?mode=control&operation=stop");},
	Next : function(){hitPath("./?mode=control&operation=next");},
	Prev : function(){hitPath("./?mode=control&operation=prev");},
	Pause : function(){hitPath("./?mode=control&operation=pause");},
	Quit : function(){hitPath("./?mode=control&operation=quit");}
};

/*
 初期化
*/
function init() {
//	setInterval(loadData, 1000);
	loadData();
}

/*
 プレーヤの現在の状態を読み込む
*/
function loadData() {
    var ajax = new Ajax();
    ajax.open("GET", "./?mode=xml");
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
			}
			$("coverArt").src = "./?mode=cover&dummy=" + Math.random();
        }
        var playlistQuery = getTagValue(xml,"playlistQuery");
        if(playlistQuery !== _playlistQuery){
        	_playlistQuery = playlistQuery;
        	loadPlaylist();
        }
        if(loadDataTimer) clearTimeout(loadDataTimer);
        loadDataTimer = setTimeout(loadData, 1000);
    }
    ajax.send(null);
}

/*
 playlistを読み込む
*/
function loadPlaylist(){
    var ajax = new Ajax();
    ajax.open("GET", "./?mode=xml&type=playlist");
    ajax.onload = function (xmlhttp) {
		var xml = xmlhttp.responseXML;
		if(xml == null)return;
		var items = xml.getElementsByTagName("item");

        var ifr = document.getElementById("playlistViewIframe");
        var doc = ifr.contentDocument || ifr.contentWindow.document;
        doc.body.innerHTML = "";

        var ul = doc.createElement("ul");
        ul.className = "playlistItemWrap";
        for(var i=0;i<items.length;i++){
        	var item = items[i];
        	var li = doc.createElement("li");
        	li.className = "playlistItem";
        	li.innerText = li.textContent = item.getAttribute("tagAlbum") + " - " + item.getAttribute("tagTitle") + "/" + item.getAttribute("tagArtist");
        	var index = item.getAttribute("index");
        	li.onclick = (function(i){
        		return function(){
		            $("infoView").innerHTML = "Loading...";
		            setTimeout(function(){hitPath("./?mode=control&operation=playitem&index=" + i);},1);      		
	        	}
        	})(i);
        	ul.appendChild(li);
        }
        doc.body.appendChild(ul);
    }
    ajax.send(null);
}

/*
 createPlaylistを実行する
*/
function commitQuery(query){
	hitPath("./?mode=control&operation=createPlaylist&query=" + encodeURIComponent(escape(query)));
}

/*
 指定したURLをGETしにいくだけ。API叩き用
*/
function hitPath(path){
	var ajax = new Ajax();
	ajax.open("GET", path);
	ajax.send(null);
    if(loadDataTimer) clearTimeout(loadDataTimer);
    loadDataTimer = setTimeout(loadData, 400);
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
	xmlhttp.onload = function(){
		if(onload_fired)return;
		onload_fired = true;
		self.onload(xmlhttp);
	}
	xmlhttp.onreadystatechange = function(){
		if(xmlhttp.readyState == 4 && xmlhttp.status == 200){
			xmlhttp.onload();
		}
	}
}

function $(id) {
    return document.getElementById(id);
}			