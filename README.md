# SiteCore Audit Logging

This Solution will allow you to Log Changes in Sitecore Made by Content Authors

## Sitecore 8.1 Audit Logging Solution

This solution allow you to track the following action that are committed by Content Authors:
* CREATE
* SAVE
* DELETE
* COPY
* MOVE
* RENAME
* SORT
* TEMPLATE CHANGE
* PUBLISH

# Package installation
To install this package in your Sitecore instance:
- Visit the update & installation wizard on your site http://[instance]/sitecore/admin/showconfig.aspx
- Upload and install the .update package /deployment/Installtion Package/Sitecore.SharedSource.Audit.update
- Once installed, in your data/logs folder you will find a audit.log.[time-date].log file.

# Visual Studio integration

## Step 1 - Add Custom code to the Solution

In your Visual Studio create the following folders.

In your project create a folder and call it  "Custom" inside that folder create a folder called "Diagnostics" and place the "Audit.cs" inside the folder.

## Step 2 - Add a Reference to "SiteCore.Logging.dll"

How to do that?

Right Click on "References" in your project and select "Add References"

In the following window you will see "Browse" option. Click on it and search for Sitecore.Logging.dll in your Project. It should be in there, select it and link to it.

## Step 3 - Build the Solution

Right click on your Project in the Visual Studio and select "Build" option.

Your solution should build without any errors.

## Step 4 - Moving DLL files in your SiteCore Site

Right Click on the bin folder in your Solution in Visual Studio and "Open Folder in File Explorer". Select all the ".dll" files and copy them.

* PLEASE BACK UP YOUR STUFF
* BEFORE OVERWRITING YOUR DLL's stop your web site in IIS.

Then go ahead to your "/inetpub/wwroot/[SiteCore Site]/Website/bin" path and overrite the ".dll" files in there.

## Step 5 - Making Changes to your SiteCore.config file

Go to "/inetpub/wwroot/[SiteCore Site]/Website/App_Config" folder and open "SiteCore.config" file and make 3 changes.

### Step 5.1 - Pipelines

Add the following code in Pipelines Section
```
<processor type="Custom.Diagnostics.Audit, [Your Project Name]" />
```
* Without []
* Project name is what you have it named in your Visual Studio

### Step 5.2 - Appender

Add the following code in Appender Section

```
<appender name="AuditLogFileAppender" type="log4net.Appender.SitecoreLogFileAppender, Sitecore.Logging">
  <file value="$(dataFolder)/logs/audit.log.{date}.txt" />
  <appendToFile value="true" />
  <layout type="log4net.Layout.PatternLayout">
	<conversionPattern value="%4t %d{ABSOLUTE} %-5p %m%n" />
  </layout>
  <encoding value="utf-8" />
</appender>
```

### Step 5.3 - Logger

Add the following code in Logger Section

```
<logger name="Sitecore.Diagnostics.Auditing" additivity="false">
  <level value="INFO" />
  <appender-ref ref="AuditLogFileAppender" />
</logger>
```


Congratulations you are done! 
Start your SiteCore Instance and start editing and tracking your logs. 
Now you should have an extra log file in your logs folder named <audit.log.{date}>.

## Acknowledgement

* Adam HerrNeckar - *[Initial Work](http://info.exsquared.com/ex-squared-blog/logging-changes-in-sitecore-made-by-content-authors#web_config)*
* Chris Auer

Please feel free to ask questions!
