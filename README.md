# SqlToFileCopy

## OVERVIEW

SqlToFileCopy is a simple winforms utility for extracting files that are saved somewhere, but whose path is stored in a sql server database. 

## FEATURES

* Connect to any Sql Sever database, just enter a connection string
* Execute any query to select the paths of the files
* Copy the files from local drives, UNC shares or HTTP resources to any local/UNC file share
* Maintain the folder structure of the copied files
* Option to use SQL to specify both the source and destination paths
* Skip missing or already copied files
* Skip missing files
* Report progress, useful for 1000s of files
* Give a summary of the copy at the end in case there were any issues

## USES

Designed for Devs/Ops type people dealing with files organised via a Sql Server (like photos in a blog CMS say)

## DOWNLOAD

Get the latest binaries from https://github.com/alastairtree/SqlToFileCopy/releases or compile from source yourself.

## SAMPLE

Say you have: a Table in Sql Server with a list of files like this:

	CREATE TABLE dbo.Files (Id int PRIMARY KEY, Path varchar(500))
	INSERT INTO dbo.Files VALUES (1, '\\Server\Folder\File.txt')
	INSERT INTO dbo.Files VALUES (2, '\\Server\Folder\File2.txt')
	INSERT INTO dbo.Files VALUES (3, '\\Server\Folder2\File3.txt')

And a folder with files somewhere like this:

	\\Server
	    \Folder
		    \File.txt
			\File2.txt
			\File_I_Dont_Care_About.txt
	    \Folder2
		    \File3.txt

Then SqlToFileCopy will allow you to query the table and copy the files you want to another folder, say C:\temp, whilst maintaining folder structure for you.

So your destination folder C:\Temp would become

	C:\Temp
	    \Folder
		    \File.txt
			\File2.txt
	    \Folder2
		    \File3.txt
