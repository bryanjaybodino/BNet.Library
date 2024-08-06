Easy Backend Pagination for ASP.NET MVC with Customizable Properties.
This library is based on the WebForms GridView.

This Library Helps you to create a pagination on the backend.

Notes : You Muse know how to use Entity Framework for Binding the Models to the Object


<div class="richText-editor" id="richText-8wgh5" contenteditable="true" style="height: auto;"><div>BNET Pagination Library for ASP.Net Core MVC&nbsp;</div><div><b><br>Version : .<font color="#ff0000">Net 5 or Higher</font></b><br></div><div><b>Features :</b><ul><li>First And Last Page Buttons</li><li>Page Entry Information</li><li>Customize Pagination using CSS</li><li>Customizable RowSize and PageSize<br></li></ul><h2>
Documentations<font color="#4f81bd"><br></font></h2><h4><font color="#4f81bd">GridView</font> gridView = new <font color="#4f81bd">GridView</font>();</h4><h4><table class="MsoNormalTable" border="1" cellspacing="0" cellpadding="0" width="1147" style="width: 573.3pt; background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial; border-collapse: collapse; border: none;">
<tbody><tr>
<td style="border-width: 1pt; padding: 7.5pt;">
<div><span style="font-size:14.0pt;mso-bidi-font-size:11.0pt;
  line-height:115%">Code<o:p></o:p></span></div>
</td>
<td style="border-top-width: 1pt; border-right-width: 1pt; border-bottom-width: 1pt; border-left: none; padding: 7.5pt;">
<div><span style="font-size:14.0pt;mso-bidi-font-size:11.0pt;
  line-height:115%">DataTypes<o:p></o:p></span></div>
</td>
<td style="border-top-width: 1pt; border-right-width: 1pt; border-bottom-width: 1pt; border-left: none; padding: 7.5pt;">
<div><span style="font-size:14.0pt;mso-bidi-font-size:11.0pt;
  line-height:115%">Details<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>start<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">int<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">starting index of current pagination<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>end<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">int<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">last row of current pagination<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>CSS_Pagination<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">string<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">pagination button container<br>
( default bootstrap <font color="#f79646">css </font>:&nbsp;<font color="#4f81bd">pagination</font>&nbsp;&nbsp;)<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>CSS_Button<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">string<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">you can edit the design of pagination button&nbsp;&nbsp;<br>
( default bootstrap <font color="#f79646">css</font> :&nbsp;<font color="#4f81bd">page-item page-link btn rounded-0</font>&nbsp;)<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>CSS_PageIndex<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">string<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">current page index<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>FirstAndLast<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">bool<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">insert a first and last page button<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>pagination<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">HtmlString<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">call this code in your view page to show the pagination
buttons<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>page_entry<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">HtmlString<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">call this code in your view page to show the overview
result of your data<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>table<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">object<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">this is your list. use this code to call you data in your
Models<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>PaginationChanged<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">EventHandler<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">this is the pagination button click event handler, it will
execute once you click your buttons<o:p></o:p></span></div>
</td>
</tr>
<tr>
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;">
<div>NewPagination<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">Method<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;">
<div><span style="font-weight: normal;">Call this inside your PaginationChanged EventHandler and
Bind your New Object<o:p></o:p></span></div>
</td>
</tr>
<tr style="mso-yfti-irow:12;mso-yfti-lastrow:yes;height:1.25pt">
<td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt; height: 1.25pt;">
<div>SetGridView<span style="font-weight: normal;"><o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt; height: 1.25pt;">
<div><span style="font-weight: normal;">Method<o:p></o:p></span></div>
</td>
<td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt; height: 1.25pt;">
<div><span style="font-weight: normal;">Call This in your Page Load and Set your Pagination
Properties</span><o:p></o:p></div>
</td>
</tr>
</tbody></table></h4></div><br></div>


Github Repository
1. https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ASP.MVC.Pagination.Sample
2. https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ASP.MVC.Pagination

Tagalog Tutorial for Version 1.0.0
https://www.youtube.com/watch?v=Y4ki37Uof1o
