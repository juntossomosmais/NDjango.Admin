// Sidebar filter
document.addEventListener('DOMContentLoaded', function () {
    var searchInput = document.getElementById('sidebar-search');
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            var filter = this.value.toLowerCase();
            var items = document.querySelectorAll('.sidebar-model-item');
            items.forEach(function (item) {
                var text = item.textContent.toLowerCase();
                item.style.display = text.indexOf(filter) !== -1 ? '' : 'none';
            });
        });
    }

    // Popup dismiss delegation — handles clicks on .popup-select links
    // rendered in the popup list view instead of inline onclick handlers.
    document.addEventListener('click', function (e) {
        var link = e.target.closest('.popup-select');
        if (!link) return;
        e.preventDefault();
        if (window.opener && typeof window.opener.dismissRelatedLookupPopup === 'function') {
            window.opener.dismissRelatedLookupPopup(window, link.getAttribute('data-pk'));
        }
    });
});

function showRelatedObjectLookupPopup(triggerLink) {
    var inputId = triggerLink.id.replace(/^lookup_/, '');
    var href = triggerLink.href;
    if (href.indexOf('?') === -1) href += '?';
    var win = window.open(href, 'lookup_' + inputId, 'height=500,width=800,resizable=yes,scrollbars=yes');
    if (win) {
        win.focus();
    }
    return false;
}

function dismissRelatedLookupPopup(win, chosenId) {
    var inputId = win.name.replace(/^lookup_/, '');
    var input = document.getElementById(inputId);
    if (input) {
        input.value = chosenId;
    }
    win.close();
}
