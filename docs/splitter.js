window.OnLoadCallback = () => {
	gapi.client.setApiKey('AIzaSyDuekF_Hf7i2GqDZ_6ExQ1Iyfn_P_-ACkg');
}

$(document).ready(function () {
	// find elements
	var buttonSplit = $("#btn-split");
	var buttonSearch = $("#btn-search");
	var max_duration_mins = 25;
	var split_api = 'https://spleeter.eastus.cloudapp.azure.com/yt';

	// handle click and add class
	buttonSplit.on("click", function(){
	  let vid = validateUrl();
	  let format = $("#type").val();
	  if (vid === null || format === null) {
		return;
	  }

	  startWait();
	  
	  // Validate duration
	  getYoutubeVideoDuration(vid, function(dur) {
		let durationInMinutes = parseInt(dur.split(':')[0]) * 60 + parseInt(dur.split(':')[1]);
		if (dur == "00:00:00") {
			alert("Can't process live videos");
		  stopWait();
		  return;
		}
		if (durationInMinutes > max_duration_mins) {
			alert("Video duration must be less than " + max_duration_mins + " minutes");
		  stopWait();
		  return;
		}
		
		// Split !
		split(vid, format);
		
		
	  });
	});

	buttonSearch.on("click", function() {
		let q = $("#search").val();
	  if (!q) {
		return;
	  }
	  if (q.length < 3) {
		return;
	  }
	  $(this).attr("disabled", true);
	  $('#search-results').empty();
	  let restRequest = gapi.client.request({
		'path': 'youtube/v3/search',
		'params': {'q': q, 'part': 'snippet', 'maxResults': 25, 'type': 'video'}
	  });
	  restRequest.execute(function(resp) { 
		  //$('#info').text(JSON.stringify(resp.items[0].id.videoId));
		  for (let i in resp.items){
				if (resp.items[i].id.videoId) {
				$('<div/>', {
					id: 'result' + i,
					"class": 'clickable',
					title: resp.items[i].id.videoId
				}).appendTo('#search-results');
				$('<img/>', {
						style: 'vertical-align:middle',
					src: resp.items[i].snippet.thumbnails.default.url
				}).appendTo('#result' + i);
				$('<span/>', {
					html: resp.items[i].snippet.title
				}).appendTo('#result' + i);
				//$('#search-results').append('<iframe width="105" height="79" src="//www.youtube.com/embed/'+ resp.items[i].id.videoId +'" frameborder="0" allowfullscreen></iframe>');    
			  }
		  }
	  });
	  $(this).removeAttr("disabled");  
	});

	$(document).on('mouseenter', '.clickable', function() {
		$(this).css("opacity", ".5");
	});
	$(document).on('mouseleave', '.clickable', function() {
	  $(this).css("opacity", "1");
	});

	$(document).on('click', '.clickable', function() {
		let vid = $(this).attr('title');
	  if (vid) {
		  $("#url").val(vid);
		  $("#btn-split").focus();
		  getYoutubeVideoDuration(vid, function(dur)
		  {
			$("#duration").text("Duration: " + dur);
			let durationInMinutes = parseInt(dur.split(':')[0]) * 60 + parseInt(dur.split(':')[1]);
			if (durationInMinutes > max_duration_mins) {
				$("#duration").css("color", "red");
			} else {
				$("#duration").css("color", "black");
			}
		  });

	  }
	});	
	
	$('#url').keypress(function (e) {
	 var key = e.which;
	 if(key == 13)  // the enter key code
	  {
		$('#btn-split').click();
		return false;  
	  }
	});   
	$('#search').keypress(function (e) {
	 var key = e.which;
	 if(key == 13)  // the enter key code
	  {
		$('#btn-search').click();
		return false;  
	  }
	}); 	
});

function validateUrl() {
	let url = $("#url").val();
  if (url.length == 0) {
  	return null;
  }
  if (url.includes(".")) {
    if (!url.toLowerCase().includes("youtu.be") && !url.toLowerCase().includes("youtube.com")) {
      alert("Invalid URL. Not a valid youtube URL");
      return null;
    }
    if (url.toLowerCase().includes("youtu.be")) {
    	let matches = url.match(/youtu\.be\/([^\?]*)/);
      if (matches) {
      	return matches[1];
      } else {
      	alert("Cannot parse video ID from youtu.be URL");
      }
    }
    else {
    	let matches = url.match(/v=([^&]*)/);
      if (matches) {
      	return matches[1];
      } else {
      	alert("Cannot parse video ID from youtube.com URL");
      }
    }
  }
  if (url.length < 10) {
  	alert("Invalid URL");
    return null;
  }
  if (url.length > 12) {
  	alert("Invalid youtube video ID");
    return null;
  }
	return url;
}

function getYoutubeVideoDuration(vid, callback) {
  let restRequest = gapi.client.request({
    'path': 'youtube/v3/videos',
    'params': {'id': vid, 'part': 'contentDetails'}
  });
  restRequest.execute(function(resp) { 
  	if (resp && resp.items && resp.items.length > 0 && resp.items[0].contentDetails) {
  		callback(YTDuration(resp.items[0].contentDetails.duration));
    } else {
    	stopWait();
      alert("Video ID not found. Reponse: " + JSON.stringify(resp));
    }
  });

}

function YTDuration(duration) {
  var match = duration.match(/PT(\d+H)?(\d+M)?(\d+S)?/);
  match = match.slice(1).map(function(x) {
    if (x != null) {
        return x.replace(/\D/, '');
    }
  });
  var hours = (parseInt(match[0]) || 0);
  var minutes = (parseInt(match[1]) || 0);
  var seconds = (parseInt(match[2]) || 0);
  let result = ("0" + hours).slice(-2) + ":" + ("0" + minutes).slice(-2) + ":" + ("0" + seconds).slice(-2);
  return result;
}

function startWait() {
	$("#spinner").show();
  $("#btn-split").hide();
	$("#btn-split").attr('disabled', true);
  $("#div-main").find("*").addClass('wait');
}
function stopWait() {
	$("#spinner").hide();
  $("#btn-split").show();
	$("#btn-split").removeAttr('disabled');
	$("#div-main").find("*").removeClass('wait');
}

function split(vid, format) {
  // WORK !
  var processUrl = split_api + "/p/" + format + "/" + vid + "?includeOriginalAudio=true";
  $("#btn-split").blur();
    
  $.ajax({
    url: processUrl,
    type: 'GET',
    success: function(data){ 
      stopWait();
      if (data.error) {
      	alert(data.error);
      } else {
      	console.log("Successful split " + data.fileId);
    		window.open(split_api + "/d/" + format + "/" + vid);
      }
    },
    error: function(jqXHR, textStatus, errorThrown) {
        stopWait();
      	alert("ERROR: " + JSON.stringify(jqXHR));
    }
	});
  
}
