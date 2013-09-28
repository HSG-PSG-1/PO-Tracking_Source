PO-Tracking_Source
==================

PO Tracking web app for AOT

# Stages of development 
-  Stage#1: Get the basic DB & proj architecture setup. Things like base classes, helper \ utility libraries, some reusable code templates, Master-page, common scripts / styles, etc...
-  Stage#2: DAL and basic Business object service classes for DB interaction. User, Master, etc... Populate some basic master data for testing. Also Includes DB changes.
-  Stage#3: Initial work flow framework session\config\setting. Login, initial security & access control, lookup, etc...
-  Stage#4: Dashboard - search, paging, sorting, KO scripts, dialogs. Controller level integration and KO/ajax action hookup. DAL function access to fetch data, etc...
-------------- to be assessed -------------- 
Stage#5: PO Entry
Stage#6: Testing & changes. Preparation for production release & Data import.


# Estimation & Sequence of development  
(also set in milestones)

## By 12th-Aug (v0.1)
- Base framework (Stage#1) : completed around 50-60% to get started.
Finalize DB, DAL and basic business classes (Milestone#2 & 3) : completed around 30-40% based on CPM blue-print.
 - Need to import some data to get started with testing the features - in prog.
- Github proj and establish sync locally & on RS 163 - pending.

## By 14th-Aug (v0.2)
- Dashboard along with excel export, dialogs, navigation and other features. I'm planning a v0.2 if I can achieve a clean code to setup and if you want to review it.

## By 17th-Aug (v0.3)
- User maintenance, list and other user specific requirements (i.e. esp for role base access, lookup, etc..)

## By 25th-Aug (v0.4)
- Master data maintenance (i.e. redo existing with the CPM like KO based grid). 
- Manage security and other pending security attributes. Manage settings.

## v0.5 (To be assessed by v0.4)
- PO edit - we've the screen and diff tabs (some are readonly), need to review more at field/data level. Need to ensure that the core framework, security, session / setting like things are ready to be used in this feature.
