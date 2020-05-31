var buttonSplit = $("#btn-split");

var max_duration_mins = 30;

var split_yt_api = 'https://spleeter-gpu2.eastus.cloudapp.azure.com/yt';
var split_mp3_api = 'https://spleeter-gpu2.eastus.cloudapp.azure.com/mp3';

var selectedFiles = [];
var dropzone;
var dzError = false;

window.OnLoadCallback = () => {
    let k = getCookie("spleeter_gapikey");
    if (k) {
        gapi.client.setApiKey(k);
    } else {
        try {
            k = CryptoJS.AES.decrypt("U2FsdGVkX1/YO06ep/mFGZGtIcASWlhidpcerOBsLehPAijwiWuK4mK7AFlx/VY19QAXtEvtEusr6nNGUcJ/Fg==", ("uoyk" + "cuf").split("").reverse().join("")).toString(CryptoJS.enc.Utf8);
        } finally {
            gapi.client.setApiKey(k);
            setCookie("spleeter_gapikey", k);
        }
    }
    $("#div-search").show();
    $("#extra-buttons").show();
};

$(document).ready(function () {
    TestConnectivity();

    makeTabs();
    setupDropFilesBox();

    $("#type").on('change', function () {
        let format = $(this).val();
        $("#div-stems div:not(._" + format + ")").hide();
        $("#div-stems div._" + format).show();
    });

    setInputsFromCookie();

    $("#btn-close-wait").on("click", function () {
        stopWait();
    });

    // handle Youtube/File Split click 
    buttonSplit.on("click", onSplit);

    // handle Search click 
    $("#btn-search").on("click", onYoutubeSearch);

    $(document).on('mouseenter', '.clickable, .file-clickable', function () {
        $(this).css("opacity", ".5");
    });
    $(document).on('mouseleave', '.clickable, .file-clickable', function () {
        $(this).css("opacity", "1");
    });

    // Handle click on video from search
    $(document).on('click', '.clickable', function () {
        let vid = $(this).attr('title');
        if (vid) {
            $("#url").val(vid);
            $("#accordion").accordion("option", "active", false);
            $("#btn-split").focus();
            getYoutubeVideoDuration(vid, function (dur, title) {
                $("#duration").text(dur);
                $("#video-title").text(title);
                $("#video-info").show();
                let durationInMinutes = parseInt(dur.split(':')[0]) * 60 + parseInt(dur.split(':')[1]);
                if (durationInMinutes > max_duration_mins) {
                    $("#duration").css("color", "red");
                } else {
                    $("#duration").css("color", "black");
                }
                $("#duration").show();
            });
        }
    });

    $('#url').keypress(function (e) {
        var key = e.which;
        if (key === 13) // the enter key code
        {
            $('#btn-split').click();
            return false;
        }
    });

    $('#search').keypress(function (e) {
        var key = e.which;
        if (key === 13) // the enter key code
        {
            $('#btn-search').click();
            return false;
        }
    });

    $("#type").change();
    $("#div-stems").show();
});

function setInputsFromCookie() {
    let formatConfig = getCookie('spleeter_format');
    if (formatConfig) {
        if (formatConfig.endsWith("stems")) {
            $("#type").val(formatConfig);
        } else {
            $("#type").val("4stems");
        }
    } else {
        $("#type").val("4stems");
    }

    let hfConfig = getCookie('spleeter_hf');
    $("#chk-hf").prop('checked', hfConfig === 'true');

    let stemsConfig = getCookie('spleeter_stems');
    if (stemsConfig) {
        $("#div-stems input").prop('checked', false);
        stemsConfig.split(',').forEach(stem => $("#div-stems input#toggle-" + stem).prop('checked', true));
    }

    let outputConfig = getCookie('spleeter_output');
    $("input#rad-" + outputConfig).prop('checked', true);
}

function setCookieFromInput() {
    let format = $("#type").val();
    setCookie('spleeter_format', format, 30);
    setCookie('spleeter_hf', $("#chk-hf").is(':checked') ? 'true' : 'false', 30);

    var stems = [];
    $("#div-stems input").filter(":checked").each(function () {
        stems.push(this.value);
    });
    setCookie('spleeter_stems', stems.join(','));

    let extension = $("input[name='output-type']:checked").val();
    if (extension) {
        setCookie('spleeter_output', extension.slice(1));
    }
    
}

function makeTabs() {
    // Show the first tab and hide the rest
    $('#tabs-nav li:first-child').addClass('active');
    $('.tab-content').hide();
    $('.tab-content:first').show();

    // Click function
    $('#tabs-nav li').click(function () {
        $('#tabs-nav li').removeClass('active');
        $(this).addClass('active');
        $('.tab-content').hide();

        var activeTab = $(this).find('a').attr('href');
        $(activeTab).fadeIn();

        let isYoutube = $("#tab1").is(":visible");
        if (isYoutube) {
            $("#container-stems").show();
            $("#container-output").show();
            if (getCookie("spleeter_gapikey")) {
                $("#extra-buttons").show();
            }
        } else {
            $("#container-stems").hide();
            $("#container-output").hide();
            $("#extra-buttons").hide();
        }

        return false;
    });
}

async function onYoutubeSearch() {
    let q = $("#search").val();
    if (!q) {
        return;
    }
    if (q.length < 3) {
        return;
    }
    $(this).attr("disabled", true);
    $('#search-results').empty();

    try {
        let request = await gapi.client.request({
            'path': 'youtube/v3/search',
            'params': {
                'q': q,
                'part': 'snippet',
                'maxResults': 20,
                'type': 'video',
                'videoCaption': $("#chk-cc").is(':checked') ? 'closedCaption' : 'any'
            }
        });
        let resp = request.result;
        // Handle response
        for (let i in resp.items) {
            if (resp.items[i].id.videoId) {
                $('<div/>', {
                    id: 'result' + i,
                    "class": 'clickable',
                    title: resp.items[i].id.videoId
                }).appendTo('#search-results');
                $('<img/>', {
                    src: resp.items[i].snippet.thumbnails.default.url
                }).appendTo('#result' + i);
                $('<span/>', {
                    html: resp.items[i].snippet.title
                }).appendTo('#result' + i);
                //$('#search-results').append('<iframe width="105" height="79" src="//www.youtube.com/embed/'+ resp.items[i].id.videoId +'" frameborder="0" allowfullscreen></iframe>');    
            }
        }
        $('#search-results').css('height', '');
        $('#search-results').show();
        $('#search-section').show();
        $("#accordion").accordion({ collapsible: true, header: "#accordion-header", active: 0 });
    } catch (e) {
        if (e.result.error.errors[0].reason === "keyInvalid") {
            removeCookie("spleeter_gapikey");
            alert("Invalid YouTube API key");
            location.reload();
        } else {
            alert(e.result.error.errors[0].message);
        }
    }
    $(this).removeAttr("disabled");
}

function validateUrl() {
    let url = $("#url").val();
    if (url.length === 0) {
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
        } else {
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

async function getYoutubeVideoDuration(vid, callback) {
    let request = await gapi.client.request({
        'path': 'youtube/v3/videos',
        'params': {
            'id': vid,
            'part': 'contentDetails,snippet'
        }
    });
    let resp = request.result;
    if (resp && resp.items && resp.items.length > 0 && resp.items[0].contentDetails) {
        callback(YTDuration(resp.items[0].contentDetails.duration), resp.items[0].snippet.title);
    } else {
        stopWait();
        alert("Video ID not found. Reponse: " + JSON.stringify(resp));
    }
}

function YTDuration(duration) {
    var match = duration.match(/PT(\d+H)?(\d+M)?(\d+S)?/);
    match = match.slice(1).map(function (x) {
        if (x !== undefined && x !== null) {
            return x.replace(/\D/, '');
        }
    });
    var hours = parseInt(match[0]) || 0;
    var minutes = parseInt(match[1]) || 0;
    var seconds = parseInt(match[2]) || 0;
    let result = ("0" + hours).slice(-2) + ":" + ("0" + minutes).slice(-2) + ":" + ("0" + seconds).slice(-2);
    return result;
}

function startWait() {
    $("#spinner").show();
    $("div.dz-preview").css("z-index", "0");
    $("#wait-dialog").modal({
        escapeClose: false,
        clickClose: false,
        showClose: false,
        fadeDuration: 100
    });


    $("#btn-split").hide();
    $("#btn-file-split").hide();
    $("#btn-split").attr('disabled', true);
    $("#div-main").find("*").addClass('wait');
}

function stopWait() {
    $("div.dz-preview").css("z-index", "auto");
    $("#spinner").hide();
    $("#btn-split").show();
    $("#btn-file-split").show();
    $("#btn-split").removeAttr('disabled');
    $("#div-main").find("*").removeClass('wait');
    $("#duration").hide();
    $("#video-info").hide();
    $.modal.close();
    $("#wait-dialog").hide();
}

function setCookie(name, value, days) {
    return localStorage.setItem(name, value);
}

function getCookie(name) {
    return localStorage.getItem(name);
}

function removeCookie(name) {
    localStorage.removeItem(name);
}

function setupDropFilesBox() {
    $("#uploader")
        .addClass('dropzone');
    dropzone = new Dropzone("#uploader", {
        url: split_mp3_api + '/p',
        paramName: "file",
        maxFilesize: 12, // MB
        maxFiles: 5,
        timeout: 600000,
        clickable: true,
        acceptedFiles: ".mp3",
        uploadMultiple: true,
        createImageThumbnails: false,
        parallelUploads: 5,
        autoProcessQueue: false,
        dictDefaultMessage: "Drop .mp3 files or click to upload",
        successmultiple: onFileSplitCompleted,
        errormultiple: function (f, errorMessage) {
            if (!dzError) {
                dzError = true;
                stopWait();
                alert("Some files cannot be processed:\n" + errorMessage);
            }
        }
    });
}

function onSplit() {
    let isYoutube = $("#tab1").is(":visible");
    if (isYoutube) {
        onYoutubeSplit();
    } else {
        onFileSplit();
    }
}

// Send mp3 files to process
function onFileSplit() {
    if (dropzone.getQueuedFiles().length === 0) {
        return;
    }
    let format = $("#type").val();
    $("#file-format").val(format);
    $("#file-hf").val($("#chk-hf").is(':checked'));

    startWait();

    setCookieFromInput();

    dzError = false;
    dropzone.processQueue(); 
}
function onFileSplitCompleted(f, response) {
    stopWait();
    dropzone.removeAllFiles();
    if (response.error) {
        dzError = true;
        alert(response.error);
    } else {
        // download file
        console.log("Successful split: " + JSON.stringify(response));
        let downloadUrl = split_mp3_api + "/d?fn=" + encodeURIComponent(response.fileId);
        window.open(downloadUrl);
    }
}

function onYoutubeSplit() {
    let vid = validateUrl();
    if (!vid) {
        return;
    }
    let format = $("#type").val();
    if (vid === null || format === null) {
        alert("Please select a video and format");
        return;
    }
    setCookieFromInput();

    // Split !
    split(vid, format);
}

// Send youtube video to process
function split(vid, format) {
    let processUrl = split_yt_api + "/p";
    let subFormats = $("#div-stems input:visible").filter(":checked").map(function () { return this.value; }).get();
    if (subFormats.length === 0) {
        alert("Must select at least one stem");
        return;
    }
    startWait();

    let extension = $("input[name='output-type']:checked").val();
    if (!extension) {
        extension = ".zip";
    }
    let includeHf = $("#chk-hf").is(':checked');
    $("#btn-split").blur();

    $.ajax({
        url: processUrl,
        type: 'POST',
        dataType: 'json',
        contentType: 'application/json',
        data: JSON.stringify({
            vid: vid,
            baseFormat: $("#type").val(),
            subFormats: subFormats,
            extension: extension,
            options: {
                includeHighFrequencies: includeHf
            }
        }),
        success: function (data) {
            stopWait();
            if (data.error) {
                alert(data.error);
            } else {
                console.log("Successful split: " + JSON.stringify(data));
                let queryString = "?sub=" + subFormats.join(',') + "&ext=" + extension + "&hf=" + includeHf;
                let downloadUrl = split_yt_api + "/d/" + format + "/" + vid + queryString;
                window.open(downloadUrl);
            }
        },
        error: function (jqXHR, textStatus, errorThrown) {
            stopWait();
            alert("Error processing " + vid + ":\n" + (jqXHR.responseText ? jqXHR.responseText : textStatus));
        }
    });
}

function presetClick(preset) {
    if (preset === "audio-karaoke") {
        $("#type").val("2stems");
        $("#type").change();
        $("#div-stems input").prop('checked', false);
        $("#div-stems input#toggle-accompaniment").prop('checked', true);
        $("input#rad-mp3").prop('checked', true);
    }
    else if (preset === "video-karaoke") {
        $("#type").val("2stems");
        $("#type").change();
        $("#div-stems input").prop('checked', false);
        $("#div-stems input#toggle-accompaniment").prop('checked', true);
        $("input#rad-mp4").prop('checked', true);
    }
	else if (preset === "audio-vocals") {
        $("#type").val("2stems");
        $("#type").change();
        $("#div-stems input").prop('checked', false);
        $("#div-stems input#toggle-vocals").prop('checked', true);
        $("input#rad-mp3").prop('checked', true);
	}
	else if (preset === "default") {
        $("#type").val("4stems");
        $("#type").change();
        $("#div-stems input").prop('checked', true);
        $("input#rad-zip").prop('checked', true);
	}
    return false;
}

function originalDownloadClick(type) {
    let vid = validateUrl();
    if (!vid) {
        return false;
    }
    let downloadUrl;
    if (type === "audio") {
        downloadUrl = split_yt_api + "/dda/" + vid;
    } else {
        downloadUrl = split_yt_api + "/ddv/" + vid;
    }
    window.open(downloadUrl);
    return false;
}

function TestConnectivity() {
    let testUrl = split_yt_api.replace("/yt", "/test");
    $("#server-down-text").html('');
    var opts = {
        method: 'GET',
        headers: {}
    };
    fetchTimeout(testUrl, opts, 4000)
        .then(function (response) {
            $("#server-down-div").toggle(!response.ok);
            return response.json();
        })
        .then(json => {
            let ip = json.ClientIp;
            let geo = json.ClientGeo;
            if (ip && ip.length > 4) {
                $("#client-geo").html("Your IP is: " + ip + " (" + geo + ")");
            }
        })
        .catch(function (error) {
            $("#server-down-div").show();
            $("#server-down-text").html(error);
        });

}

function fetchTimeout(url, options, timeout) {
    return Promise.race([
        fetch(url, options),
        new Promise((_, reject) =>
            setTimeout(() => reject(new Error('timeout')), timeout)
        )
    ]);
}