# SqlCompare
SqlDataCompare

Simple program to compare sql tables.

* Add the connection strings on the app.config

<pre>
  SqlDataCompare  [db1] [db2] [table] (dstFile)
</pre>

Where

* db1: The name of the source connection string
* db2: The name of the destination connection string
* table: The name of the table in format schema.tableName
* dstFile(optional): The destination file. Will be written on the executable directory unless is an absolute path

## If you like it Buy me a coffe :)

[![paypal](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/paypalme/kendarorg/1)

# Results

<pre>
Compare table '[dbo].[Items]' from 'SrcData' with 'DstData'

Missing field 'IsOnOff' on destination.

Different
~ (GroupId=1,Name=Other,PackageId=4,SortOrder=50,Type=Core) FROM
  (GroupId=1,Name=Other,PackageId=4,SortOrder=50,Type=Ancillary)

Missing on source
- (-) FROM (GroupId=1,Name=Graphics,PackageId=2,SortOrder=50,Type=Core)

Missing on destionation
+ (GroupId=1,Name=Extra,PackageId=8,SortOrder=10,Type=Smart) FROM (-)
</pre>


