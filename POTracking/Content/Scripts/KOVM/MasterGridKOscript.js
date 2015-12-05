﻿var IsCCEditMode = false;
var NextNewrecordID = 0;
var recordsViewModel = function () {
    var self = this;
    self.emptyrecord = "";

    self.allRecords = ko.observableArray();
    self.newRecord = ko.observable();
    
    self.setEdited = function (record) {
        record.IsUpdated(!record.IsAdded());
        record.LastModifiedDate(Date111); 
    }
    
    self.setEditedFlag = function (record) {
        record.IsUpdated(!record.IsAdded());
        record.LastModifiedDate(Date111);
        
        var titl = record.Code();
        if(titl == "") { alert("This field is required");}
        else if($.grep(self.allRecords(), function(el)
                {
                    return (!el.IsDeleted() && el.Code().toLowerCase() === titl.toLowerCase());}
               ).length > 1)
            { alert("Duplicate entry found"); }
        else
           { record.CodeOLD(titl); return;}

        record.Code(record.CodeOLD()); // rollback
    }
    self.addNewRecord = function () {
        var ID = self.newRecord.ID();
        self.newRecord.ID(NextNewrecordID);

        self.newRecord.Code(self.newRecord.Code().replace(ID, NextNewrecordID-1));
        self.newRecord.CodeOLD(self.newRecord.Code());
        self.allRecords.push((cloneObservable(self.newRecord)));

        NextNewrecordID = NextNewrecordID - 1;
        self.newRecord.ID(NextNewrecordID);        
        
        return true;
    };
    
    self.removeSelected = function (record) {
        if (record != null)
        {
            record.IsDeleted(true);
            if (record.IsAdded()) {                
                self.allRecords.remove(record);
                //record.IsAdded(false);
            }
        }
    };

    self.unRemoveSelected = function (record) {
        if (record != null) // Prevent blanks and duplicates
            record.IsDeleted(false);        
    };

    self.cancelrecord = function (record) {
        IsCCEditMode = false;        
        self.recordToAdd(cloneObservable(self.emptyrecord));
    };

    self.saveToServer = function () {
    var jsonData = ko.mapping.toJSON({ "records" : ko.mapping.toJS(self.allRecords) });
    $("input[type=button]").attr('disabled','disabled');
        $.ajax({ // SO: 12846689
            url: location.href,
            type: "POST",
            dataType: "json",
            //processData: false,
            contentType: "application/json; charset=utf-8",
            data: jsonData,
            success: function(data) {
                if(data != null && data.length > 0)
                {
                    alert(data);
                    $("input[type=button]").removeAttr('disabled');
                }
                else
                {   //alert("Operation was successful"); 
                    window.location.href = window.location.href; // Refresh page
                }
                $("input[type=button]").removeAttr('disabled');
            }
        });
        // ko.utils.postJson(location.href, jsonData ); //{ records: ko.mapping.toJS(self.allRecords) });
        return false;
    }
};
var vmMaster = new recordsViewModel();

function createRecordsKO()
{
    showDlg(true);
    $.getJSON(manageKOVMURL + '?masterTbl=' + getSelMaster(),
        function (data) { showDlg(false);
             var newRec;
             if(data != null && data.length > 0)
               {newRec = data.pop(); newRec.IsAdded = true;}
            
            vmMaster.allRecords = ko.mapping.fromJS(data);// Make sure the last record is removed
            vmMaster.newRecord = ko.mapping.fromJS(newRec);            
            ko.applyBindings(vmMaster);

            setFocus("ddlMaster");
        });
}

$(document).ready(function () {
    createRecordsKO();
    //setFocus("record1");
});

function getSelMaster()
{
    return $("#ddlMaster").children("option").filter(":selected").text();
}