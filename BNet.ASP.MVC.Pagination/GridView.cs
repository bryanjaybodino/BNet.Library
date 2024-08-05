using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BNet.ASP.MVC.Pagination
{
    public class GridView
    {
        public delegate Task<IActionResult> PaginationEventHandler(string NewPageIndex);
        public event PaginationEventHandler PaginationChanged;
        int max_page { get; set; } = 0;
        static int page_sequence { get; set; } = 1;
        int max_page_sequence { get; set; } = 0;
        int page_index { get; set; } = 0;
        int page_start { get; set; } = 0;
        int page_end { get; set; } = 0;
        int page_offset { get; set; } = 0;
        public int start { get; set; } = 0;
        public int end { get; set; } = 0;
        public object table { get; set; } = new object();
        public HtmlString pagination { get; private set; }
        public HtmlString page_entry { get; private set; }
        public bool FirstAndLast { get; set; } = false;
        public string CSS_Pagination { get; set; } = "pagination";
        public string CSS_Button { get; set; } = "page-item page-link btn rounded-0";
        public string CSS_PageIndex { get; set; } = "bg-primary text-white ";
        string TableId { get; set; } = "";
        string Route { get; set; } = "";
        string scriptName { get; set; } = "";

        private void Set<T>(List<T> dataTable, int RowSize, int PageSize, string setPageIndex)
        {
            scriptName = TableId + "_PaginationChange";

            if (setPageIndex == "0")
            {
                page_sequence = 1;
            }
            setPageIndex = (setPageIndex == "") ? "0" : setPageIndex;
            max_page = (int)Math.Ceiling((double)dataTable.Count / RowSize);
            int first_page = 1;

            max_page_sequence = (int)Math.Ceiling((double)max_page / PageSize);

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

            page_start = (page_sequence - 1) * PageSize;
            page_end = PageSize * page_sequence;
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

            page_offset = (page_index * RowSize) + RowSize;
            page_offset = (page_offset > dataTable.Count) ? dataTable.Count : page_offset;


            start = (page_index * RowSize);
            end = RowSize * (page_index + 1);
            table = dataTable;

            //table = dataTable.AsEnumerable()
            //.Skip(0)
            //.Take(page_offset)
            //.CopyToDataTable();

            int result = (dataTable.Count > 0) ? ((page_index * RowSize) + 1) : 0;
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
                    Pagination += "<a style='display:block' onclick=\"" + scriptName + "('FIRST')\" class=\"" + CSS_Button + "\"  " + action + " >" + first_page + "</a>";
                    Pagination += "<span style='padding:7px;display:block'>_</span>";
                }
                Pagination += "<a style='display:block' onclick=\"" + scriptName + "('BACK')\" class=\"" + CSS_Button + "\">Back</a>";
            }


            for (int i = page_start; i < page_end; i++)
            {
                action = (page_index == i) ? CSS_PageIndex : "";
                Pagination += "<a  onclick=\"" + scriptName + "(" + i + ")\" class=\"" + CSS_Button + " " + action + "\">" + (i + 1) + "</a>";
            }

            if (page_end != max_page)
            {
                Pagination += "<a style='display:block' onclick=\"" + scriptName + "('NEXT')\" class=\"" + CSS_Button + "\">Next</a>";
                if (FirstAndLast == true)
                {
                    Pagination += "<span style='padding:7px;display:block'>_</span>";
                    Pagination += "<a style='display:block' onclick=\"" + scriptName + "('LAST')\" class=\"" + CSS_Button + "\"  " + action + " >" + max_page + "</a>";

                }
            }

            Pagination += "</div>";

            Pagination += "<script>";
            Pagination += "\nfunction  " + scriptName + "(pageindex='0')" +
                "\n" +
                "\n{" +
                "\nvar url = document.location.origin + '/" + Route + "?NewPageIndex='+pageindex;" +
                "\nfetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' } })" +
                "\n.then(response => response.text())" +
                "\n.then(responseText => {" +
                "\nconst parser = new DOMParser();" +
                "\nconst doc = parser.parseFromString(responseText, 'text/html');" +
                "\nconst TableContent = doc.getElementById('" + TableId + "').innerHTML;" +
                "\ndocument.getElementById('" + TableId + "').innerHTML = TableContent;" +
                "\n})" +
                "\n.catch(error => {" +
                "\ndocument.body.innerHTML = '<h4>INVALID HTTP REQUEST</h4>' + '<br>' + error.message;" +
                "\n });" +
                "\n}" +
                "";
            Pagination += "\n</script>";

            pagination = new HtmlString(Pagination);

        }
        public void SetGriView<T>(List<T> MyDataList, string RouteName, string TableName, int RowSize, int PageSize, string NewPageIndex = "0")
        {
            TableId = TableName;
            Route = RouteName;
            Set(MyDataList, RowSize, PageSize, NewPageIndex);
        }
    }
}
