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
});
