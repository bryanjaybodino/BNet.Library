using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BNet.ASP.MVC.Pagination
{
    public class GridView
    {
        // ── Events ────────────────────────────────────────────────────────────
        public class PaginationChangedEventArgs : EventArgs
        {
            public string NewPageIndex { get; set; }
            public string TableName { get; set; }
            public string RouteName { get; set; }
        }

        public delegate Task<IActionResult> PaginationEventHandler(PaginationChangedEventArgs e);
        public event PaginationEventHandler PaginationChanged;

        // ── Public configuration ───────────────────────────────────────────
        public string CSS_Pagination { get; set; } = "pagination";
        public string CSS_Button { get; set; } = "page-item page-link btn rounded-0";
        public string CSS_PageIndex { get; set; } = "bg-primary text-white";
        public bool FirstAndLast { get; set; } = false;

        // ── Public read-only output ────────────────────────────────────────
        public int Start { get; private set; }
        public int End { get; private set; }
        public object Table { get; private set; } = new object();
        public HtmlString Pagination { get; private set; }
        public HtmlString PageEntry { get; private set; }

        // ── Private pagination state ───────────────────────────────────────
        private int _maxPage = 0;
        private int _maxPageSequence = 0;
        private int _pageIndex = 0;
        private int _pageStart = 0;
        private int _pageEnd = 0;
        private int _pageOffset = 0;
        private int _pageSequence = 1;
        private int _rowSize = 0;
        private int _pageSize = 0;
        private string _tableId = string.Empty;
        private string _route = string.Empty;
        private string _scriptName = string.Empty;

        // ── Session key helpers ────────────────────────────────────────────
        private static string PageSequenceKey(string tableId) => $"{tableId}_page_sequence";
        private static string RowSizeKey(string tableId) => $"{tableId}_rowsize";
        private static string PageSizeKey(string tableId) => $"{tableId}_pagesize";
        private static string FirstLastKey(string tableId) => $"{tableId}_first_and_last";

        // ── Public entry points ────────────────────────────────────────────

        /// <summary>Initialises the grid for a first render.</summary>
        public void SetGridView<T>(
            HttpContext context,
            List<T> dataList,
            string routeName,
            string tableName,
            int rowSize,
            int pageSize)
        {
            _tableId = tableName;
            _route = routeName;
            BuildPagination(context, dataList, rowSize, pageSize, pageIndex: "0");
            LoadSession(context);
        }

        /// <summary>Updates the grid after a pagination action.</summary>
        public void NewPagination<T>(
            HttpContext context,
            List<T> dataTable,
            PaginationChangedEventArgs e)
        {
            _tableId = e.TableName;
            _route = e.RouteName;
            LoadSession(context);
            BuildPagination(context, dataTable, _rowSize, _pageSize, e.NewPageIndex);
        }

        // ── Session management ─────────────────────────────────────────────

        private void LoadSession(HttpContext context)
        {
            var session = context.Session;

            _pageSequence = GetOrSet(session, PageSequenceKey(_tableId), _pageSequence);
            _rowSize = GetOrSet(session, RowSizeKey(_tableId), _rowSize);
            _pageSize = GetOrSet(session, PageSizeKey(_tableId), _pageSize);

            var firstLastRaw = session.GetString(FirstLastKey(_tableId));
            if (firstLastRaw is null)
                session.SetString(FirstLastKey(_tableId), FirstAndLast.ToString().ToLower());
            else
                FirstAndLast = bool.TryParse(firstLastRaw, out var parsed) && parsed;
        }

        private void SaveSession(HttpContext context)
        {
            var session = context.Session;
            session.SetInt32(PageSequenceKey(_tableId), _pageSequence);
            session.SetInt32(RowSizeKey(_tableId), _rowSize);
            session.SetInt32(PageSizeKey(_tableId), _pageSize);
            session.SetString(FirstLastKey(_tableId), FirstAndLast.ToString().ToLower());
        }

        private static int GetOrSet(ISession session, string key, int defaultValue)
        {
            if (!session.TryGetValue(key, out _))
            {
                session.SetInt32(key, defaultValue);
                return defaultValue;
            }
            return session.GetInt32(key) ?? defaultValue;
        }

        // ── Core pagination builder ────────────────────────────────────────

        private void BuildPagination<T>(
            HttpContext context,
            List<T> dataTable,
            int rowSize,
            int pageSize,
            string pageIndex)
        {
            _rowSize = rowSize;
            _pageSize = pageSize;
            _scriptName = $"{_tableId}_PaginationChange";

            // Normalise input
            if (pageIndex == "0") _pageSequence = 1;
            if (string.IsNullOrEmpty(pageIndex)) pageIndex = "0";

            // Totals
            _maxPage = (int)Math.Ceiling((double)dataTable.Count / _rowSize);
            _maxPageSequence = (int)Math.Ceiling((double)_maxPage / _pageSize);

            ApplySequenceNavigation(pageIndex);

            _pageStart = (_pageSequence - 1) * _pageSize;
            _pageEnd = Math.Min(_pageSize * _pageSequence, _maxPage);

            ApplyIndexNavigation(pageIndex);

            _pageOffset = Math.Min((_pageIndex * _rowSize) + _rowSize, dataTable.Count);
            Start = _pageIndex * _rowSize;
            End = _rowSize * (_pageIndex + 1);
            Table = dataTable;

            PageEntry = BuildPageEntry(dataTable.Count);
            Pagination = BuildPaginationHtml(context);

            SaveSession(context);
        }

        private void ApplySequenceNavigation(string pageIndex)
        {
            switch (pageIndex.ToUpperInvariant())
            {
                case "NEXT": _pageSequence = Math.Min(_pageSequence + 1, _maxPageSequence); break;
                case "BACK": _pageSequence = Math.Max(_pageSequence - 1, 1); break;
                case "FIRST": _pageSequence = 1; break;
                case "LAST": _pageSequence = _maxPageSequence; break;
            }
        }

        private void ApplyIndexNavigation(string pageIndex)
        {
            switch (pageIndex.ToUpperInvariant())
            {
                case "NEXT":
                case "BACK":
                case "FIRST":
                    _pageIndex = _pageStart;
                    break;
                case "LAST":
                    _pageIndex = _maxPage - 1;
                    break;
                default:
                    _pageIndex = int.TryParse(pageIndex, out var parsed) ? parsed : 0;
                    break;
            }
        }

        // ── HTML builders ──────────────────────────────────────────────────

        private HtmlString BuildPageEntry(int totalCount)
        {
            int from = totalCount > 0 ? (_pageIndex * _rowSize) + 1 : 0;
            return new HtmlString(
                $"<div>Showing {from:N0} to {_pageOffset:N0} of {totalCount:N0} entries</div>");
        }

        private HtmlString BuildPaginationHtml(HttpContext context)
        {
            var sb = new StringBuilder();
            sb.Append($"<div class=\"{CSS_Pagination}\">");

            // Back / First buttons
            if (_pageSequence > 1)
            {
                if (FirstAndLast)
                {
                    sb.Append(NavButton("FIRST", "1"));
                    sb.Append("<span style='padding:7px;display:block'>_</span>");
                }
                sb.Append(NavButton("BACK", "Back"));
            }

            // Numbered page buttons
            for (int i = _pageStart; i < _pageEnd; i++)
            {
                string active = (_pageIndex == i) ? CSS_PageIndex : string.Empty;
                sb.Append($"<a onclick=\"{_scriptName}({i}, '{_tableId}','{_route}')\" " +
                          $"class=\"{CSS_Button} {active}\">{i + 1}</a>");
            }

            // Next / Last buttons
            if (_pageEnd != _maxPage)
            {
                sb.Append(NavButton("NEXT", "Next"));
                if (FirstAndLast)
                {
                    sb.Append("<span style='padding:7px;display:block'>_</span>");
                    sb.Append(NavButton("LAST", _maxPage.ToString()));
                }
            }

            sb.Append("</div>");
            sb.Append(BuildPaginationScript());
            sb.Append(BuildSearchScript(context));

            return new HtmlString(sb.ToString());
        }

        private string NavButton(string action, string label) =>
            $"<a style='display:block' onclick=\"{_scriptName}('{action}', '{_tableId}','{_route}')\" " +
            $"class=\"{CSS_Button}\">{label}</a>";

        // ── Script builders ────────────────────────────────────────────────

        private string BuildPaginationScript() => $@"
<script>
function {_scriptName}(pageindex, tableid, route) {{
    pageindex = pageindex ?? '0';
    var url = `${{document.location.origin}}/${{route}}?NewPageIndex=${{pageindex}}&TableName=${{tableid}}&RouteName=${{route}}`;
    fetch(url, {{ method: 'POST', headers: {{ 'Content-Type': 'application/json' }} }})
        .then(r => r.text())
        .then(html => {{
            const doc = new DOMParser().parseFromString(html, 'text/html');
            document.getElementById(tableid).innerHTML = doc.getElementById(tableid).innerHTML;
        }})
        .catch(err => {{
            document.body.innerHTML = '<h4>INVALID HTTP REQUEST</h4><br>' + err.message;
        }});
}}
</script>";

        private string BuildSearchScript(HttpContext context)
        {
            var controller = context.GetRouteData().Values["controller"]?.ToString() ?? string.Empty;
            return $@"
<script>
function {_tableId}_SearchEvent(params) {{
    const queryString = Object.keys(params).map(k => k + '=' + encodeURIComponent(params[k])).join('&');
    const url = `${{document.location.origin}}/{controller}?${{queryString}}`;
    fetch(url, {{ method: 'GET', headers: {{ 'Content-Type': 'application/json' }} }})
        .then(r => {{
            if (!r.ok) return r.text().then(t => {{ throw new Error(t); }});
            return r.text();
        }})
        .then(html => {{
            const doc = new DOMParser().parseFromString(html, 'text/html');
            document.getElementById('{_tableId}').innerHTML = doc.getElementById('{_tableId}').innerHTML;
        }})
        .catch(err => {{
            document.body.innerHTML = '<h4>INVALID HTTP REQUEST</h4><br>' + err.message;
        }});
}}
</script>";
        }
    }
}