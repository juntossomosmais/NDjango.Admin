/**
 * @jest-environment jsdom
 */

let adminDashboard;

beforeEach(() => {
    document.body.innerHTML = '';
    jest.restoreAllMocks();
    delete window.opener;
});

function loadScript() {
    jest.resetModules();
    adminDashboard = require('./admin-dashboard');
    document.dispatchEvent(new Event('DOMContentLoaded'));
}

// ─── Sidebar filter ───

describe('sidebar filter', () => {
    test('filters items matching the search input', () => {
        document.body.innerHTML = `
            <input id="sidebar-search" />
            <div class="sidebar-model-item">Restaurants</div>
            <div class="sidebar-model-item">Categories</div>
            <div class="sidebar-model-item">Menu Items</div>
        `;
        loadScript();

        const input = document.getElementById('sidebar-search');
        input.value = 'cat';
        input.dispatchEvent(new Event('input'));

        const items = document.querySelectorAll('.sidebar-model-item');
        expect(items[0].style.display).toBe('none');
        expect(items[1].style.display).toBe('');
        expect(items[2].style.display).toBe('none');
    });

    test('shows all items when filter is cleared', () => {
        document.body.innerHTML = `
            <input id="sidebar-search" />
            <div class="sidebar-model-item">Restaurants</div>
            <div class="sidebar-model-item">Categories</div>
        `;
        loadScript();

        const input = document.getElementById('sidebar-search');
        input.value = 'zzz';
        input.dispatchEvent(new Event('input'));
        input.value = '';
        input.dispatchEvent(new Event('input'));

        const items = document.querySelectorAll('.sidebar-model-item');
        expect(items[0].style.display).toBe('');
        expect(items[1].style.display).toBe('');
    });

    test('works when sidebar-search element is absent', () => {
        document.body.innerHTML = '';
        expect(() => loadScript()).not.toThrow();
    });
});

// ─── Bulk action checkbox management ───

function buildChangelistHTML({ withToggle = true, withCounter = true, withForm = true, checkboxCount = 3 } = {}) {
    let html = '';
    if (withToggle) html += '<input type="checkbox" id="action-toggle" />';
    if (withCounter) html += '<span class="action-counter"></span>';
    if (withForm) {
        html += '<form id="changelist-form">';
        html += `<select name="action">
            <option value="">---------</option>
            <option value="delete_selected">Delete selected</option>
            <option value="allow_empty" data-allow-empty="true">Allow empty</option>
        </select>`;
        html += '<table><tbody>';
        for (let i = 0; i < checkboxCount; i++) {
            html += `<tr><td><input type="checkbox" class="action-select" value="${i}" /></td></tr>`;
        }
        html += '</tbody></table></form>';
    }
    return html;
}

describe('action toggle (select all)', () => {
    test('checks all checkboxes and updates counter', () => {
        document.body.innerHTML = buildChangelistHTML();
        loadScript();

        const toggle = document.getElementById('action-toggle');
        toggle.checked = true;
        toggle.dispatchEvent(new Event('change'));

        const cbs = document.querySelectorAll('.action-select');
        cbs.forEach(cb => expect(cb.checked).toBe(true));
        expect(document.querySelector('.action-counter').textContent).toBe('3 of 3 selected');
        cbs.forEach(cb => expect(cb.closest('tr').classList.contains('selected')).toBe(true));
    });

    test('unchecks all checkboxes', () => {
        document.body.innerHTML = buildChangelistHTML();
        loadScript();

        const toggle = document.getElementById('action-toggle');
        toggle.checked = true;
        toggle.dispatchEvent(new Event('change'));
        toggle.checked = false;
        toggle.dispatchEvent(new Event('change'));

        const cbs = document.querySelectorAll('.action-select');
        cbs.forEach(cb => expect(cb.checked).toBe(false));
        expect(document.querySelector('.action-counter').textContent).toBe('0 of 3 selected');
        cbs.forEach(cb => expect(cb.closest('tr').classList.contains('selected')).toBe(false));
    });
});

describe('individual checkbox change', () => {
    test('updates counter and toggle state when individual checkbox is checked', () => {
        document.body.innerHTML = buildChangelistHTML({ checkboxCount: 2 });
        loadScript();

        const cbs = document.querySelectorAll('.action-select');
        cbs[0].checked = true;
        cbs[0].dispatchEvent(new Event('change'));

        expect(document.querySelector('.action-counter').textContent).toBe('1 of 2 selected');
        expect(document.getElementById('action-toggle').checked).toBe(false);
        expect(cbs[0].closest('tr').classList.contains('selected')).toBe(true);
    });

    test('sets toggle to checked when all checkboxes are checked individually', () => {
        document.body.innerHTML = buildChangelistHTML({ checkboxCount: 2 });
        loadScript();

        const cbs = document.querySelectorAll('.action-select');
        cbs[0].checked = true;
        cbs[0].dispatchEvent(new Event('change'));
        cbs[1].checked = true;
        cbs[1].dispatchEvent(new Event('change'));

        expect(document.getElementById('action-toggle').checked).toBe(true);
    });

    test('works without action-toggle element', () => {
        document.body.innerHTML = buildChangelistHTML({ withToggle: false });
        loadScript();

        const cbs = document.querySelectorAll('.action-select');
        cbs[0].checked = true;
        cbs[0].dispatchEvent(new Event('change'));

        expect(document.querySelector('.action-counter').textContent).toBe('1 of 3 selected');
    });
});

describe('updateActionCounter when counter element is absent', () => {
    test('does not throw when action-counter is missing', () => {
        document.body.innerHTML = buildChangelistHTML({ withCounter: false });
        loadScript();

        const toggle = document.getElementById('action-toggle');
        toggle.checked = true;
        expect(() => toggle.dispatchEvent(new Event('change'))).not.toThrow();
    });
});

// ─── Form submission ───

describe('changelist form submit', () => {
    test('prevents submit when no action is selected (empty value)', () => {
        document.body.innerHTML = buildChangelistHTML();
        loadScript();

        const form = document.getElementById('changelist-form');
        const select = form.querySelector('select[name="action"]');
        select.selectedIndex = 0;

        const event = new Event('submit', { cancelable: true });
        form.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(true);
    });

    test('prevents submit when action selected but no checkboxes checked', () => {
        document.body.innerHTML = buildChangelistHTML();
        loadScript();

        const form = document.getElementById('changelist-form');
        const select = form.querySelector('select[name="action"]');
        select.selectedIndex = 1;

        jest.spyOn(window, 'alert').mockImplementation(() => {});

        const event = new Event('submit', { cancelable: true });
        form.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(true);
        expect(window.alert).toHaveBeenCalledWith('Please select at least one item.');
    });

    test('allows submit when action selected and checkboxes checked', () => {
        document.body.innerHTML = buildChangelistHTML();
        loadScript();

        const form = document.getElementById('changelist-form');
        const select = form.querySelector('select[name="action"]');
        select.selectedIndex = 1;

        const cbs = document.querySelectorAll('.action-select');
        cbs[0].checked = true;

        const event = new Event('submit', { cancelable: true });
        form.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(false);
    });

    test('allows submit with allow-empty action even when no checkboxes checked', () => {
        document.body.innerHTML = buildChangelistHTML();
        loadScript();

        const form = document.getElementById('changelist-form');
        const select = form.querySelector('select[name="action"]');
        select.selectedIndex = 2;

        const event = new Event('submit', { cancelable: true });
        form.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(false);
    });

    test('prevents submit when select element is missing from form', () => {
        document.body.innerHTML = '<form id="changelist-form"></form>';
        loadScript();

        const form = document.getElementById('changelist-form');
        const event = new Event('submit', { cancelable: true });
        form.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(true);
    });
});

describe('no changelist form', () => {
    test('does not throw when changelist-form is absent', () => {
        document.body.innerHTML = buildChangelistHTML({ withForm: false });
        expect(() => loadScript()).not.toThrow();
    });
});

// ─── Popup dismiss delegation ───

describe('popup dismiss delegation', () => {
    test('calls dismissRelatedLookupPopup on opener when clicking a .popup-select link', () => {
        document.body.innerHTML = '<a class="popup-select" data-pk="42" href="#">Pick</a>';
        loadScript();

        const mockDismiss = jest.fn();
        window.opener = { dismissRelatedLookupPopup: mockDismiss };

        const link = document.querySelector('.popup-select');
        const event = new Event('click', { bubbles: true, cancelable: true });
        link.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(true);
        expect(mockDismiss).toHaveBeenCalledWith(window, '42');
    });

    test('does nothing when click target is not a .popup-select', () => {
        document.body.innerHTML = '<a class="other-link" href="#">Other</a>';
        loadScript();

        const link = document.querySelector('.other-link');
        const event = new Event('click', { bubbles: true, cancelable: true });
        link.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(false);
    });

    test('does not throw when window.opener is null', () => {
        document.body.innerHTML = '<a class="popup-select" data-pk="7" href="#">Pick</a>';
        loadScript();

        window.opener = null;

        const link = document.querySelector('.popup-select');
        expect(() => {
            link.dispatchEvent(new Event('click', { bubbles: true, cancelable: true }));
        }).not.toThrow();
    });

    test('does not call opener when dismissRelatedLookupPopup is not a function', () => {
        document.body.innerHTML = '<a class="popup-select" data-pk="7" href="#">Pick</a>';
        loadScript();

        window.opener = { dismissRelatedLookupPopup: 'not-a-function' };

        const link = document.querySelector('.popup-select');
        expect(() => {
            link.dispatchEvent(new Event('click', { bubbles: true, cancelable: true }));
        }).not.toThrow();
    });
});

// ─── showRelatedObjectLookupPopup ───

describe('showRelatedObjectLookupPopup', () => {
    test('opens a popup window and returns false', () => {
        loadScript();

        const mockWin = { focus: jest.fn() };
        jest.spyOn(window, 'open').mockReturnValue(mockWin);

        const triggerLink = { id: 'lookup_category_id', href: 'http://localhost/admin/Category/?_popup=1' };
        const result = adminDashboard.showRelatedObjectLookupPopup(triggerLink);

        expect(result).toBe(false);
        expect(window.open).toHaveBeenCalledWith(
            'http://localhost/admin/Category/?_popup=1',
            'lookup_category_id',
            'height=500,width=800,resizable=yes,scrollbars=yes'
        );
        expect(mockWin.focus).toHaveBeenCalled();
    });

    test('appends ? to href when no query string present', () => {
        loadScript();

        const mockWin = { focus: jest.fn() };
        jest.spyOn(window, 'open').mockReturnValue(mockWin);

        const triggerLink = { id: 'lookup_restaurant_id', href: 'http://localhost/admin/Restaurant/' };
        adminDashboard.showRelatedObjectLookupPopup(triggerLink);

        expect(window.open).toHaveBeenCalledWith(
            'http://localhost/admin/Restaurant/?',
            'lookup_restaurant_id',
            expect.any(String)
        );
    });

    test('handles window.open returning null', () => {
        loadScript();

        jest.spyOn(window, 'open').mockReturnValue(null);

        const triggerLink = { id: 'lookup_x', href: 'http://localhost/admin/X/?_popup=1' };
        const result = adminDashboard.showRelatedObjectLookupPopup(triggerLink);

        expect(result).toBe(false);
    });
});

// ─── dismissRelatedLookupPopup ───

describe('dismissRelatedLookupPopup', () => {
    test('sets input value and closes popup window', () => {
        document.body.innerHTML = '<input id="category_id" />';
        loadScript();

        const mockWin = { name: 'lookup_category_id', close: jest.fn() };
        adminDashboard.dismissRelatedLookupPopup(mockWin, '99');

        expect(document.getElementById('category_id').value).toBe('99');
        expect(mockWin.close).toHaveBeenCalled();
    });

    test('closes popup even when input element is not found', () => {
        document.body.innerHTML = '';
        loadScript();

        const mockWin = { name: 'lookup_missing_id', close: jest.fn() };
        adminDashboard.dismissRelatedLookupPopup(mockWin, '1');

        expect(mockWin.close).toHaveBeenCalled();
    });
});
