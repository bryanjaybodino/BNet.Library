using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Routing;
using System.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace BNet.ASP.MVC.Pagination
{
    public class GridView
    {
        public class PaginationChangedEventArgs : EventArgs
        {
            public string NewPageIndex { get; set; }
            public string TableName { get; set; }
            public string RouteName { get; set; }
        }

        public delegate Task<IActionResult> PaginationEventHandler(PaginationChangedEventArgs e);
        public event PaginationEventHandler PaginationChanged;

        // Instance variables
        private int max_page { get; set; } = 0;
      
        private int max_page_sequence { get; set; } = 0;
        private int page_index { get; set; } = 0;
        private int page_start { get; set; } = 0;
        private int page_end { get; set; } = 0;
        private int page_offset { get; set; } = 0;

        public int start { get; set; } = 0;
        public int end { get; set; } = 0;
        public object table { get; private set; } = new object();
        public HtmlString pagination { get; private set; }
        public HtmlString page_entry { get; private set; }
      
        public string CSS_Pagination { get; set; } = "pagination";
        public string CSS_Button { get; set; } = "page-item page-link btn rounded-0";
        public string CSS_PageIndex { get; set; } = "bg-primary text-white ";
        private string TableId { get; set; } = "";
        private string Route { get; set; } = "";
        private string scriptName { get; set; } = "";


        private int page_sequence { get; set; } = 1;
        public bool FirstAndLast { get; set; } = false;
        private int _RowSize { get; set; } = 0;
        private int _PageSize { get; set; } = 0;

        private void SetSession(HttpContext context, string tableId)
        {
            string _page_sequence = tableId + "_page_sequence";
            string _rowsize = tableId + "_rowsize";
            string _pagesize = tableId + "_pagesize";
            string _first_and_last = tableId + "_first_and_last";

            var session = context.Session;

            // Helper function to get or set session values
            int GetOrSetSessionValue(string key, int defaultValue)
            {
                if (!session.TryGetValue(key, out var value))
                {
                    session.SetInt32(key, defaultValue);
                    return defaultValue;
                }
                return session.GetInt32(key).Value;
            }

            page_sequence = GetOrSetSessionValue(_page_sequence, page_sequence);
            _RowSize = GetOrSetSessionValue(_rowsize, _RowSize);
            _PageSize = GetOrSetSessionValue(_pagesize, _PageSize);

            // Handle boolean value
            if (!session.TryGetValue(_first_and_last, out var value))
            {
                // Store the boolean value as a string
                session.SetString(_first_and_last, FirstAndLast.ToString().ToLower());
            }
            else
            {
                // Retrieve the boolean value from the session
                var stringValue = session.GetString(_first_and_last);
                FirstAndLast = !string.IsNullOrEmpty(stringValue) && bool.Parse(stringValue);
            }
        }




        private void UpdateSession(HttpContext context, string tableId, int newPageSequence, int newRowSize, int newPageSize,bool newFirstLast)
        {
            // Define the session keys based on the tableId
            string _page_sequence = tableId + "_page_sequence";
            string _rowsize = tableId + "_rowsize";
            string _pagesize = tableId + "_pagesize";
            string _first_and_last = tableId + "_first_and_last";

            // Access the session
            var session = context.Session;

            // Update the session with the new values
            session.SetInt32(_page_sequence, newPageSequence);
            session.SetInt32(_rowsize, newRowSize);
            session.SetInt32(_pagesize, newPageSize);
            session.SetString(_first_and_last, newFirstLast.ToString());


            // Update the instance variables with the new values
            page_sequence = newPageSequence;
            _RowSize = newRowSize;
            _PageSize = newPageSize;
            FirstAndLast = newFirstLast;
        }

        private void Set<T>(HttpContext context, List<T> dataTable, int RowSize, int PageSize, string setPageIndex)
        {
            _RowSize = RowSize;
            _PageSize = PageSize;
            scriptName = TableId + "_PaginationChange";

            if (setPageIndex == "0")
            {
                page_sequence = 1;
            }
            setPageIndex = (setPageIndex == "") ? "0" : setPageIndex;
            max_page = (int)Math.Ceiling((double)dataTable.Count / _RowSize);
            int first_page = 1;

            max_page_sequence = (int)Math.Ceiling((double)max_page / _PageSize);

            if (setPageIndex.ToUpper() == "NEXT")
            {
                page_sequence++;
            }
            else if (setPageIndex.ToUpper() == "BACK")
            {
                page_sequence--;
            }
            else if (setPageIndex.ToUpper() == "FIRST")
            {
                page_sequence = first_page;
            }
            else if (setPageIndex.ToUpper() == "LAST")
            {
                page_sequence = max_page_sequence;
            }



            page_start = (page_sequence - 1) * _PageSize;
            page_end = _PageSize * page_sequence;
            page_end = (page_end > max_page) ? max_page : page_end;

            if (setPageIndex.ToUpper() == "NEXT" || setPageIndex.ToUpper() == "BACK" || setPageIndex.ToUpper() == "FIRST")
            {
                page_index = page_start;
            }
            else if (setPageIndex.ToUpper() == "LAST")
            {
                page_index = max_page - 1;
            }
            else
            {
                page_index = Convert.ToInt32(setPageIndex);
            }

            page_offset = (page_index * _RowSize) + _RowSize;
            page_offset = (page_offset > dataTable.Count) ? dataTable.Count : page_offset;

            start = (page_index * _RowSize);
            end = _RowSize * (page_index + 1);
            table = dataTable;

            int result = (dataTable.Count > 0) ? ((page_index * _RowSize) + 1) : 0;
            string Pagination = "<div class=\"" + CSS_Pagination + "\">";
            string PageEntry = "<div>";
            PageEntry += "Showing " + result.ToString("N0") + " to " + (page_offset).ToString("N0") + " of " + dataTable.Count.ToString("N0") + " entries";
            PageEntry += "</div>";
            page_entry = new HtmlString(PageEntry);
            string action = "";

            if (page_sequence > 1)
            {
                if (FirstAndLast == true)
                {
                    Pagination += "<a style='display:block' onclick=\"" + scriptName + "('FIRST', '" + TableId + "','" + Route + "')\" class=\"" + CSS_Button + "\"  " + action + " >" + first_page + "</a>";
                    Pagination += "<span style='padding:7px;display:block'>_</span>";
                }
                Pagination += "<a style='display:block' onclick=\"" + scriptName + "('BACK', '" + TableId + "','" + Route + "')\" class=\"" + CSS_Button + "\">Back</a>";
            }

            for (int i = page_start; i < page_end; i++)
            {
                action = (page_index == i) ? CSS_PageIndex : "";
                Pagination += "<a onclick=\"" + scriptName + "(" + i + ", '" + TableId + "','" + Route + "')\" class=\"" + CSS_Button + " " + action + "\">" + (i + 1) + "</a>";
            }
            if (page_end != max_page)
            {
                Pagination += "<a style='display:block' onclick=\"" + scriptName + "('NEXT', '" + TableId + "','" + Route + "')\" class=\"" + CSS_Button + "\">Next</a>";
                if (FirstAndLast == true)
                {
                    Pagination += "<span style='padding:7px;display:block'>_</span>";
                    Pagination += "<a style='display:block' onclick=\"" + scriptName + "('LAST', '" + TableId + "','" + Route + "')\" class=\"" + CSS_Button + "\"  " + action + " >" + max_page + "</a>";
                }
            }

            Pagination += "</div>";

            Pagination += GeneratePaginationScript(scriptName);
            Pagination += GenerateSearchEventScript(context, TableId);

            pagination = new HtmlString(Pagination);

            UpdateSession(context, TableId, page_sequence, _RowSize, PageSize,FirstAndLast);
        }
        string GeneratePaginationScript(string scriptName)
        {
            string script = "<script>\n";
            script += "function " + scriptName + "(pageindex='0',tableid='',route='') {\n";
            script += "     var url = document.location.origin + '/' +route+ '?NewPageIndex='+ pageindex+'&TableName='+tableid+'&RouteName='+route;\n";
            script += "    fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' } })\n";
            script += "        .then(response => response.text())\n";
            script += "        .then(responseText => {\n";
            script += "            const parser = new DOMParser();\n";
            script += "            const doc = parser.parseFromString(responseText, 'text/html');\n";
            script += "            const TableContent = doc.getElementById(tableid).innerHTML;\n";
            script += "            document.getElementById(tableid).innerHTML = TableContent;\n";
            script += "        })\n";
            script += "        .catch(error => {\n";
            script += "            document.body.innerHTML = '<h4>INVALID HTTP REQUEST</h4>' + '<br>' + error.message;\n";
            script += "        });\n";
            script += "}\n";
            script += "</script>";

            return script;
        }
        private string GenerateSearchEventScript(HttpContext context, string tableId)
        {
            var routeData = context.GetRouteData();
            var controller = routeData.Values["controller"];
            string route = controller.ToString();
            var sb = new StringBuilder();

            sb.AppendLine("<script>");
            sb.AppendLine($"function {tableId}_SearchEvent(params) {{");
            sb.AppendLine("    // Convert params object to query string");
            sb.AppendLine("    let queryString = Object.keys(params).map(key => key + '=' + encodeURIComponent(params[key])).join('&');");
            sb.AppendLine($"    // Construct the URL with the query string");
            sb.AppendLine($"    let url = document.location.origin + '/{route}?' + queryString;");
            sb.AppendLine("    // Make an HTTP GET request using fetch API");
            sb.AppendLine("    fetch(url, {");
            sb.AppendLine("        method: 'GET',");
            sb.AppendLine("        headers: {");
            sb.AppendLine("            'Content-Type': 'application/json'");
            sb.AppendLine("        }");
            sb.AppendLine("    })");
            sb.AppendLine("    .then(response => {");
            sb.AppendLine("        if (!response.ok) {");
            sb.AppendLine("            return response.text().then(text => {");
            sb.AppendLine("                throw new Error(text);");
            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine("        return response.text();");
            sb.AppendLine("    })");
            sb.AppendLine("    .then(responseText => {");
            sb.AppendLine("        // Parse the response text to find the HTML content");
            sb.AppendLine("        let parser = new DOMParser();");
            sb.AppendLine("        let doc = parser.parseFromString(responseText, 'text/html');");
            sb.AppendLine($"        let result = doc.getElementById('{tableId}').innerHTML;");
            sb.AppendLine($"        document.getElementById('{tableId}').innerHTML = result;");
            sb.AppendLine("    })");
            sb.AppendLine("    .catch(error => {");
            sb.AppendLine("        // Handle any errors that occurred during the fetch");
            sb.AppendLine("        document.body.innerHTML = '<h4>INVALID HTTP REQUEST</h4>' + '<br>' + error.message;");
            sb.AppendLine("    });");
            sb.AppendLine("}");
            sb.AppendLine("</script>");

            return sb.ToString();
        }
        public void SetGridView<T>(HttpContext context, List<T> MyDataList, string RouteName, string TableName, int RowSize, int PageSize)
        {
            TableId = TableName;
            Route = RouteName;
            Set(context, MyDataList, RowSize, PageSize, "0");
            SetSession(context, TableId);
        }

        public void NewPagination<T>(HttpContext context, List<T> dataTable, PaginationChangedEventArgs e)
        {
            TableId = e.TableName;
            Route = e.RouteName;

            SetSession(context, TableId);
            Set(context, dataTable, _RowSize, _PageSize, e.NewPageIndex);
        }
    }
}
