// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

String.format = function () {
    var s = arguments[0];
    for (var i = 0; i < arguments.length - 1; i++) {
        var reg = new RegExp("\\{" + i + "\\}", "gm");
        s = s.replace(reg, arguments[i + 1]);
    }

    return s;
}
function submitAjaxPost(url, data, onSuccess, onError) {
    $.ajax({
        type: "POST",
        headers: {
            "RequestVerificationToken": $('input:hidden[name="__RequestVerificationToken"]').first().val()
        },
        url: url,
        data: data,
        success: function (data, status, xhr) {
            if (onSuccess)
                onSuccess(data, status, xhr);
        },
        error: function (req, status, error) {
            if (onError)
                onError(req, status, error);
        }
    });
}
function submitPost(url, data, frmId) {
    var _frmId = frmId;
    if (_frmId.indexOf("#") < 0)
        _frmId = '#' + frmId;
    var keys = Object.keys(data);
    var values = Object.values(data);
    if ($(_frmId).length === 0) {
        var frmHtml = String.format("<form method='post' id='{0}'>", _frmId.replace('#', ''));
        for (var i = 0; i < keys.length; i++) {
            frmHtml += String.format("<input type='hidden' name='{0}' />", keys[i])
        }
        frmHtml += "</form>";
        $('body').append(frmHtml);
    }
    $(_frmId).find('input:hidden[name!="__RequestVerificationToken"]').val("");

    var token = $('input:hidden[name="__RequestVerificationToken"]').first().val();
    for (var j = 0; j < keys.length; j++) {
        $(_frmId).find(String.format("input:hidden[name='{0}']", keys[j])).val(values[j]);
    }
    if (token) {
        if ($(_frmId).find('input:hidden[name="__RequestVerificationToken"]').length === 0) {
            $(_frmId).append("<input type='hidden' name='__RequestVerificationToken' />");
        }
        $(_frmId).find('input:hidden[name="__RequestVerificationToken"]').val(token);
    }
    $(_frmId).attr('action', url);
    $(_frmId).submit();
}