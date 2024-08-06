Easy Backend Pagination for ASP.NET MVC with Customizable Properties.
This library is based on the WebForms GridView.

This Library Helps you to create a pagination on the backend.

Notes : You Muse know how to use Entity Framework for Binding the Models to the Object


<div class="richText-toolbar"><ul><li><a class="richText-btn bi bi-type-bold h4" data-command="bold" title="Bold"><span class=""></span></a></li><li><a class="richText-btn bi bi-type-italic h4" data-command="italic" title="Italic"><span class=""></span></a></li><li><a class="richText-btn bi bi-type-underline h4" data-command="underline" title="Underline"><span class=""></span></a></li><li><a class="richText-btn bi bi-justify-left h4" data-command="justifyLeft" title="Align left"><span class=""></span></a></li><li><a class="richText-btn bi bi-text-center h4" data-command="justifyCenter" title="Align centered"><span class=""></span></a></li><li><a class="richText-btn bi bi-justify-right h4" data-command="justifyRight" title="Align right"><span class=""></span></a></li><li><a class="richText-btn bi bi-justify h4" data-command="justifyFull" title="Justify"><span class=""></span></a></li><li><a class="richText-btn bi bi-list-ol h4" data-command="insertOrderedList" title="Ordered list"><span class=""></span></a></li><li><a class="richText-btn bi bi-list-ul h4" data-command="insertUnorderedList" title="Unordered list"><span class=""></span></a></li><li><a class="richText-btn bi-bounding-box h4" style="text-decoration:none" title="Font size"><span class=""></span><div class="richText-dropdown-outer"><ul class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><li><a style="font-size:24px;" data-command="fontSize" data-option="24">Text 24px</a></li><li><a style="font-size:18px;" data-command="fontSize" data-option="18">Text 18px</a></li><li><a style="font-size:16px;" data-command="fontSize" data-option="16">Text 16px</a></li><li><a style="font-size:14px;" data-command="fontSize" data-option="14">Text 14px</a></li><li><a style="font-size:12px;" data-command="fontSize" data-option="12">Text 12px</a></li></ul></div></a></li><li><a class="richText-btn bi bi-type-h1 h4" title="Heading/title"><span class=" "></span><div class="richText-dropdown-outer"><ul class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><li><a data-command="formatBlock" data-option="h1">Title #1</a></li><li><a data-command="formatBlock" data-option="h2">Title #2</a></li><li><a data-command="formatBlock" data-option="h3">Title #3</a></li><li><a data-command="formatBlock" data-option="h4">Title #4</a></li></ul></div></a></li><li><a class="richText-btn bi bi-palette h4" title="Font color"><span class=" "></span><div class="richText-dropdown-outer"><ul class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><li class="inline"><a data-command="forecolor" data-option="#FFFFFF" style="text-align:left;" title="White"><span class="box-color" style="background-color:#FFFFFF"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#000000" style="text-align:left;" title="Black"><span class="box-color" style="background-color:#000000"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#7F6000" style="text-align:left;" title="Brown"><span class="box-color" style="background-color:#7F6000"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#938953" style="text-align:left;" title="Beige"><span class="box-color" style="background-color:#938953"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#1F497D" style="text-align:left;" title="Dark Blue"><span class="box-color" style="background-color:#1F497D"></span></a></li><li class="inline"><a data-command="forecolor" data-option="blue" style="text-align:left;" title="Blue"><span class="box-color" style="background-color:blue"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#4F81BD" style="text-align:left;" title="Light Blue"><span class="box-color" style="background-color:#4F81BD"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#953734" style="text-align:left;" title="Dark Red"><span class="box-color" style="background-color:#953734"></span></a></li><li class="inline"><a data-command="forecolor" data-option="red" style="text-align:left;" title="Red"><span class="box-color" style="background-color:red"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#4F6128" style="text-align:left;" title="Dark Green"><span class="box-color" style="background-color:#4F6128"></span></a></li><li class="inline"><a data-command="forecolor" data-option="green" style="text-align:left;" title="Green"><span class="box-color" style="background-color:green"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#3F3151" style="text-align:left;" title="Purple"><span class="box-color" style="background-color:#3F3151"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#31859B" style="text-align:left;" title="Dark Turquois"><span class="box-color" style="background-color:#31859B"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#4BACC6" style="text-align:left;" title="Turquois"><span class="box-color" style="background-color:#4BACC6"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#E36C09" style="text-align:left;" title="Dark Orange"><span class="box-color" style="background-color:#E36C09"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#F79646" style="text-align:left;" title="Orange"><span class="box-color" style="background-color:#F79646"></span></a></li><li class="inline"><a data-command="forecolor" data-option="#FFFF00" style="text-align:left;" title="Yellow"><span class="box-color" style="background-color:#FFFF00"></span></a></li></ul></div></a></li><li><a class="richText-btn bi bi-paint-bucket h4" title="Background color"><span class=" "></span><div class="richText-dropdown-outer"><ul class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><li class="inline"><a data-command="hiliteColor" data-option="#FFFFFF" style="text-align:left;" title="White"><span class="box-color" style="background-color:#FFFFFF"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#000000" style="text-align:left;" title="Black"><span class="box-color" style="background-color:#000000"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#7F6000" style="text-align:left;" title="Brown"><span class="box-color" style="background-color:#7F6000"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#938953" style="text-align:left;" title="Beige"><span class="box-color" style="background-color:#938953"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#1F497D" style="text-align:left;" title="Dark Blue"><span class="box-color" style="background-color:#1F497D"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="blue" style="text-align:left;" title="Blue"><span class="box-color" style="background-color:blue"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#4F81BD" style="text-align:left;" title="Light Blue"><span class="box-color" style="background-color:#4F81BD"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#953734" style="text-align:left;" title="Dark Red"><span class="box-color" style="background-color:#953734"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="red" style="text-align:left;" title="Red"><span class="box-color" style="background-color:red"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#4F6128" style="text-align:left;" title="Dark Green"><span class="box-color" style="background-color:#4F6128"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="green" style="text-align:left;" title="Green"><span class="box-color" style="background-color:green"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#3F3151" style="text-align:left;" title="Purple"><span class="box-color" style="background-color:#3F3151"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#31859B" style="text-align:left;" title="Dark Turquois"><span class="box-color" style="background-color:#31859B"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#4BACC6" style="text-align:left;" title="Turquois"><span class="box-color" style="background-color:#4BACC6"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#E36C09" style="text-align:left;" title="Dark Orange"><span class="box-color" style="background-color:#E36C09"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#F79646" style="text-align:left;" title="Orange"><span class="box-color" style="background-color:#F79646"></span></a></li><li class="inline"><a data-command="hiliteColor" data-option="#FFFF00" style="text-align:left;" title="Yellow"><span class="box-color" style="background-color:#FFFF00"></span></a></li></ul></div></a></li><li><a class="richText-btn bi bi-table h4" title="Add table"><span class=""></span><div class="richText-dropdown-outer"><div class="richText-dropdown"><span class="richText-dropdown-close"><span title="Close"><span class="fa fa-times"></span></span></span><div class="richText-form" id="richText-Table" data-editor="richText-8wgh5"><div class="richText-form-item"><label for="tableRows">Rows</label><input type="number" id="tableRows"></div><div class="richText-form-item"><label for="tableColumns">Columns</label><input type="number" id="tableColumns"></div><div class="richText-form-item"><button class="btn" type="button">Add</button></div></div></div></div></a></li></ul></div>



Github Repository
1. https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ASP.MVC.Pagination.Sample
2. https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ASP.MVC.Pagination

Tagalog Tutorial for Version 1.0.0
https://www.youtube.com/watch?v=Y4ki37Uof1o
