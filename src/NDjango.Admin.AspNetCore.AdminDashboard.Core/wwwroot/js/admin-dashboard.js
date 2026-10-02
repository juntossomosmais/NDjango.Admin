// Sidebar filter
document.addEventListener('DOMContentLoaded', function () {
    const searchInput = document.getElementById('sidebar-search');
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            const filter = this.value.toLowerCase();
            const items = document.querySelectorAll('.sidebar-model-item');
            items.forEach(function (item) {
                const text = item.textContent.toLowerCase();
                item.style.display = text.indexOf(filter) !== -1 ? '' : 'none';
            });
        });
    }

    // Bulk action checkbox management
    const actionToggle = document.getElementById('action-toggle');
    const actionCheckboxes = document.querySelectorAll('.action-select');
    const actionCounter = document.querySelector('.action-counter');
    const changelistForm = document.getElementById('changelist-form');

    if (actionToggle) {
        actionToggle.addEventListener('change', function () {
            actionCheckboxes.forEach(function (cb) {
                cb.checked = actionToggle.checked;
                cb.closest('tr').classList.toggle('selected', cb.checked);
            });
            updateActionCounter();
        });
    }

    actionCheckboxes.forEach(function (cb) {
        cb.addEventListener('change', function () {
            this.closest('tr').classList.toggle('selected', this.checked);
            if (actionToggle) {
                actionToggle.checked = Array.from(actionCheckboxes).every(function (c) { return c.checked; });
            }
            updateActionCounter();
        });
    });

    function updateActionCounter() {
        if (!actionCounter) return;
        const checked = document.querySelectorAll('.action-select:checked').length;
        const total = actionCheckboxes.length;
        actionCounter.textContent = checked + ' of ' + total + ' selected';
    }

    if (changelistForm) {
        changelistForm.addEventListener('submit', function (e) {
            const select = changelistForm.querySelector('select[name="action"]');
            if (!select?.value) {
                e.preventDefault();
                return;
            }
            const selected = select.options[select.selectedIndex];
            const allowEmpty = selected.dataset.allowEmpty === 'true';
            const checkedCount = document.querySelectorAll('.action-select:checked').length;
            if (checkedCount === 0 && !allowEmpty) {
                e.preventDefault();
                alert('Please select at least one item.');
            }
        });
    }

    // Popup dismiss delegation — handles clicks on .popup-select links
    // rendered in the popup list view instead of inline onclick handlers.
    document.addEventListener('click', function (e) {
        const link = e.target.closest('.popup-select');
        if (!link) return;
        e.preventDefault();
        if (window.opener && typeof window.opener.dismissRelatedLookupPopup === 'function') {
            window.opener.dismissRelatedLookupPopup(window, link.getAttribute('data-pk'), link.getAttribute('data-label'));
        }
    });

    // A key typed by hand no longer matches the label rendered next to it; clear it rather than
    // show the label of another record. Picking through the popup fills it again.
    document.querySelectorAll('.vForeignKeyRawIdAdminField').forEach(function (input) {
        input.addEventListener('input', function () {
            const label = document.getElementById('label_' + input.id);
            if (label) label.textContent = '';
        });
    });

    focusFirstFormError();
});

function focusFirstFormError() {
    const errorRow = document.querySelector('.form-row.errors');
    if (!errorRow) return;
    if (typeof errorRow.scrollIntoView === 'function') {
        errorRow.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
    const input = errorRow.querySelector('input, textarea, select');
    if (input && typeof input.focus === 'function') {
        input.focus();
    }
}

function showRelatedObjectLookupPopup(triggerLink) {
    const inputId = triggerLink.id.replace(/^lookup_/, '');
    let href = triggerLink.href;
    if (href.indexOf('?') === -1) href += '?';
    const win = window.open(href, 'lookup_' + inputId, 'height=500,width=800,resizable=yes,scrollbars=yes');
    if (win) {
        win.focus();
    }
    return false;
}

function dismissRelatedLookupPopup(win, chosenId, chosenLabel) {
    const inputId = win.name.replace(/^lookup_/, '');
    const input = document.getElementById(inputId);
    if (input) {
        input.value = chosenId;
    }
    const label = document.getElementById('label_' + inputId);
    if (label) {
        label.textContent = chosenLabel || '';
    }
    win.close();
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { showRelatedObjectLookupPopup, dismissRelatedLookupPopup, focusFirstFormError };
}
