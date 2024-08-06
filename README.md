Easy Backend Pagination for ASP.NET MVC with Customizable Properties.
This library is based on the WebForms GridView.

This Library Helps you to create a pagination on the backend.

Notes : You Muse know how to use Entity Framework for Binding the Models to the Object


<div>
<div class="richText"><div class="richText-toolbar"><ul><li><a class="richText-btn bi bi-type-bold h4" data-command="bold" title="Bold"><span class=""></span></a></li><li><a class="richText-btn bi bi-type-italic h4" data-command="italic" title="Italic"><span class=""></span></a></li><li><a class="richText-btn bi bi-type-underline h4" data-command="underline" title="Underline"><span class=""></span></a></li><li><a class="richText-btn bi bi-justify-left h4" data-command="justifyLeft" title="Align left"><span class=""></span></a></li><li><a class="richText-btn bi bi-text-center h4" data-command="justifyCenter" title="Align centered"><span class=""></span></a></li><li><a class="richText-btn bi bi-justify-right h4" data-command="justifyRight" title="Align right"><span class=""></span></a></li><li><a class="richText-btn bi bi-justify h4" data-command="justifyFull" title="Justify"><span class=""></span></a></li><li><a class="richText-btn bi bi-list-ol h4" data-command="insertOrderedList" title="Ordered list"><span class=""></span></a></li><li><a class="richText-btn bi bi-list-ul h4" data-command="insertUnorderedList" title="Unordered list"><span class=""></span></a></li><li><a class="richText-btn bi-bounding-box h4" style="text-decoration:none" title="Font size"><span class=""></span><div class="richText-dropdown-outer"><ul class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><li><a style="font-size:24px;" data-command="fontSize" data-option="24">Text 24px</a></li><li><a style="font-size:18px;" data-command="fontSize" data-option="18">Text 18px</a></li><li><a style="font-size:16px;" data-command="fontSize" data-option="16">Text 16px</a></li><li><a style="font-size:14px;" data-command="fontSize" data-option="14">Text 14px</a></li><li><a style="font-size:12px;" data-command="fontSize" data-option="12">Text 12px</a></li></ul></div></a></li><li><a class="richText-btn bi bi-type-h1 h4" title="Heading/title"><span class=" "></span><div class="richText-dropdown-outer"><ul class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><li><a data-command="formatBlock" data-option="h1">Title #1</a></li><li><a data-command="formatBlock" data-option="h2">Title #2</a></li><li><a data-command="formatBlock" data-option="h3">Title #3</a></li><li><a data-command="formatBlock" data-option="h4">Title #4</a></li></ul></div></a></li><li><a class="richText-btn bi bi-palette h4" title="Font color"><span class=" "></span><div class="richText-dropdown-outer"><ul class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><li class="inline"><a data-command="forecolor" data-option="#FFFFFF" style="text-align:left;" title="White"><span class="box-color" style="background-color:#FFFFFF"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#000000" style="text-align:left;" title="Black"><span class="box-color" style="background-color:#000000"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#7F6000" style="text-align:left;" title="Brown"><span class="box-color" style="background-color:#7F6000"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#938953" style="text-align:left;" title="Beige"><span class="box-color" style="background-color:#938953"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#1F497D" style="text-align:left;" title="Dark Blue"><span class="box-color" style="background-color:#1F497D"></span></a></li><li class="inline"><a data-command="forecolor" data-option="blue" style="text-align:left;" title="Blue"><span class="box-color" style="background-color:blue"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#4F81BD" style="text-align:left;" title="Light Blue"><span class="box-color" style="background-color:#4F81BD"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#953734" style="text-align:left;" title="Dark Red"><span class="box-color" style="background-color:#953734"></span></a></li><li class="inline"><a data-command="forecolor" data-option="red" style="text-align:left;" title="Red"><span class="box-color" style="background-color:red"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#4F6128" style="text-align:left;" title="Dark Green"><span class="box-color" style="background-color:#4F6128"></span></a></li><li class="inline"><a data-command="forecolor" data-option="green" style="text-align:left;" title="Green"><span class="box-color" style="background-color:green"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#3F3151" style="text-align:left;" title="Purple"><span class="box-color" style="background-color:#3F3151"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#31859B" style="text-align:left;" title="Dark Turquois"><span class="box-color" style="background-color:#31859B"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#4BACC6" style="text-align:left;" title="Turquois"><span class="box-color" style="background-color:#4BACC6"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#E36C09" style="text-align:left;" title="Dark Orange"><span class="box-color" style="background-color:#E36C09"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#F79646" style="text-align:left;" title="Orange"><span class="box-color" style="background-color:#F79646"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#FFFF00" style="text-align:left;" title="Yellow"><span class="box-color" style="background-color:#FFFF00"></span></a></li></ul></div></a></li><li><a class="richText-btn bi bi-paint-bucket h4" title="Background color"><span class=" "></span><div class="richText-dropdown-outer"><ul class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><li class="inline"><a data-command="hiliteColor" data-option="#FFFFFF" style="text-align:left;" title="White"><span class="box-color" style="background-color:#FFFFFF"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#000000" style="text-align:left;" title="Black"><span class="box-color" style="background-color:#000000"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#7F6000" style="text-align:left;" title="Brown"><span class="box-color" style="background-color:#7F6000"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#938953" style="text-align:left;" title="Beige"><span class="box-color" style="background-color:#938953"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#1F497D" style="text-align:left;" title="Dark Blue"><span class="box-color" style="background-color:#1F497D"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="blue" style="text-align:left;" title="Blue"><span class="box-color" style="background-color:blue"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#4F81BD" style="text-align:left;" title="Light Blue"><span class="box-color" style="background-color:#4F81BD"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#953734" style="text-align:left;" title="Dark Red"><span class="box-color" style="background-color:#953734"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="red" style="text-align:left;" title="Red"><span class="box-color" style="background-color:red"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#4F6128" style="text-align:left;" title="Dark Green"><span class="box-color" style="background-color:#4F6128"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="green" style="text-align:left;" title="Green"><span class="box-color" style="background-color:green"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#3F3151" style="text-align:left;" title="Purple"><span class="box-color" style="background-color:#3F3151"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#31859B" style="text-align:left;" title="Dark Turquois"><span class="box-color" style="background-color:#31859B"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#4BACC6" style="text-align:left;" title="Turquois"><span class="box-color" style="background-color:#4BACC6"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#E36C09" style="text-align:left;" title="Dark Orange"><span class="box-color" style="background-color:#E36C09"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#F79646" style="text-align:left;" title="Orange"><span class="box-color" style="background-color:#F79646"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#FFFF00" style="text-align:left;" title="Yellow"><span class="box-color" style="background-color:#FFFF00"></span></a></li></ul></div></a></li><li><a class="richText-btn bi bi-table h4" title="Add table"><span class=""></span><div class="richText-dropdown-outer"><div class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><div class="richText-form" id="richText-Table" data-editor="richText-8wgh5"><div class="richText-form-item"><label for="tableRows">Rows</label><input type="number" id="tableRows"></div><div class="richText-form-item"><label for="tableColumns">Columns</label><input type="number" id="tableColumns"></div><div class="richText-form-item"><button class="btn" type="button">Add</button></div></div></div></div></a></li></ul></div><div class="richText-editor" id="richText-8wgh5" contenteditable="true" style="height: auto;"><div>BNET Pagination Library for ASP.Net Core MVC&nbsp;</div><div><b><br>Version : .<font color="#ff0000">Net 5 or Higher</font></b><br></div><div><b>Features :</b><ul><li>First And Last Page Buttons</li><li>Page Entry Information</li><li>Customize Pagination using CSS</li><li>Customizable RowSize and PageSize<br></li></ul><h2>
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
</tbody></table></h4></div><br></div><textarea name="Description" id="Description" class="rich-text richText-initial" value="" style="display: none; height: auto;">&lt;div&gt;BNET Pagination Library for ASP.Net Core MVC&nbsp;&lt;/div&gt;&lt;div&gt;&lt;b&gt;&lt;br&gt;Version : .&lt;font color="#ff0000"&gt;Net 5 or Higher&lt;/font&gt;&lt;/b&gt;&lt;br&gt;&lt;/div&gt;&lt;div&gt;&lt;b&gt;Features :&lt;/b&gt;&lt;ul&gt;&lt;li&gt;First And Last Page Buttons&lt;/li&gt;&lt;li&gt;Page Entry Information&lt;/li&gt;&lt;li&gt;Customize Pagination using CSS&lt;/li&gt;&lt;li&gt;Customizable RowSize and PageSize&lt;br&gt;&lt;/li&gt;&lt;/ul&gt;&lt;h2&gt;
Documentations&lt;font color="#4f81bd"&gt;&lt;br&gt;&lt;/font&gt;&lt;/h2&gt;&lt;h4&gt;&lt;font color="#4f81bd"&gt;GridView&lt;/font&gt; gridView = new &lt;font color="#4f81bd"&gt;GridView&lt;/font&gt;();&lt;/h4&gt;&lt;h4&gt;&lt;table class="MsoNormalTable" border="1" cellspacing="0" cellpadding="0" width="1147" style="width: 573.3pt; background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial; border-collapse: collapse; border: none;"&gt;
&lt;tbody&gt;&lt;tr&gt;
&lt;td style="border-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-size:14.0pt;mso-bidi-font-size:11.0pt;
  line-height:115%"&gt;Code&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top-width: 1pt; border-right-width: 1pt; border-bottom-width: 1pt; border-left: none; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-size:14.0pt;mso-bidi-font-size:11.0pt;
  line-height:115%"&gt;DataTypes&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top-width: 1pt; border-right-width: 1pt; border-bottom-width: 1pt; border-left: none; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-size:14.0pt;mso-bidi-font-size:11.0pt;
  line-height:115%"&gt;Details&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;start&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;int&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;starting index of current pagination&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;end&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;int&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;last row of current pagination&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;CSS_Pagination&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;string&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;pagination button container&lt;br&gt;
( default bootstrap &lt;font color="#f79646"&gt;css &lt;/font&gt;:&nbsp;&lt;font color="#4f81bd"&gt;pagination&lt;/font&gt;&nbsp;&nbsp;)&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;CSS_Button&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;string&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;you can edit the design of pagination button&nbsp;&nbsp;&lt;br&gt;
( default bootstrap &lt;font color="#f79646"&gt;css&lt;/font&gt; :&nbsp;&lt;font color="#4f81bd"&gt;page-item page-link btn rounded-0&lt;/font&gt;&nbsp;)&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;CSS_PageIndex&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;string&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;current page index&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;FirstAndLast&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;bool&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;insert a first and last page button&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;pagination&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;HtmlString&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;call this code in your view page to show the pagination
buttons&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;page_entry&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;HtmlString&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;call this code in your view page to show the overview
result of your data&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;table&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;object&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;this is your list. use this code to call you data in your
Models&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;PaginationChanged&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;EventHandler&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;this is the pagination button click event handler, it will
execute once you click your buttons&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt;"&gt;
&lt;div&gt;NewPagination&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;Method&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;Call this inside your PaginationChanged EventHandler and
Bind your New Object&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;tr style="mso-yfti-irow:12;mso-yfti-lastrow:yes;height:1.25pt"&gt;
&lt;td style="border-right-width: 1pt; border-bottom-width: 1pt; border-left-width: 1pt; border-top: none; padding: 7.5pt; height: 1.25pt;"&gt;
&lt;div&gt;SetGridView&lt;span style="font-weight: normal;"&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt; height: 1.25pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;Method&lt;o:p&gt;&lt;/o:p&gt;&lt;/span&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;td style="border-top: none; border-left: none; border-bottom-width: 1pt; border-right-width: 1pt; padding: 7.5pt; height: 1.25pt;"&gt;
&lt;div&gt;&lt;span style="font-weight: normal;"&gt;Call This in your Page Load and Set your Pagination
Properties&lt;/span&gt;&lt;o:p&gt;&lt;/o:p&gt;&lt;/div&gt;
&lt;/td&gt;
&lt;/tr&gt;
&lt;/tbody&gt;&lt;/table&gt;&lt;/h4&gt;&lt;/div&gt;&lt;br&gt;</textarea><div class="richText-toolbar"><a class="richText-undo is-disabled" title="Undo"><span class="bi bi-arrow-counterclockwise"></span></a><a class="richText-redo is-disabled" title="Redo"><span class="bi bi-arrow-clockwise"></span></a><a class="richText-help"><span class="fa fa-question-circle"></span></a></div></div>
</div>



Github Repository
1. https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ASP.MVC.Pagination.Sample
2. https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ASP.MVC.Pagination

Tagalog Tutorial for Version 1.0.0
https://www.youtube.com/watch?v=Y4ki37Uof1o
