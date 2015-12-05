﻿    var IsCCEditMode = false;
    var NextNewrecordID = 0;

    var recordsViewModel = function () {
        var self = this;
        self.emptyrecord = "";

        self.allRecords = ko.observableArray();
        self.newRecord = ko.observable();
        /*//self.jsonText = ko.computed(function() { return ("JSON: " + ko.toJSON(self.recordToAdd())); });*/

        self.setEdited = function (record) {
            record.IsUpdated(!record.IsAdded());
            record.LastModifiedDate(Date111);
        }
        self.setEditedFlag = function (record) {
            record.IsUpdated(!record.IsAdded());
            record.LastModifiedDate(Date111);

            var code = record.Code();
            if (code == "") { alert("This field is required"); }
            else if ($.grep(self.allRecords(), function (el) {
                return (!el.IsDeleted() && el.Code().toLowerCase() === code.toLowerCase());
            }
               ).length > 1)
            { alert("Duplicate entry found"); }
            else
            { record.CodeOLD(code); return; }

            record.Code(record.CodeOLD()); // rollback
        }
        self.addNewRecord = function () {
            var ID = self.newRecord.ID();
            self.newRecord.ID(NextNewrecordID);

            self.newRecord.Code(self.newRecord.Code().replace(ID, NextNewrecordID - 1));
            self.newRecord.CodeOLD(self.newRecord.Code());
            self.allRecords.push((cloneObservable(self.newRecord)));

            NextNewrecordID = NextNewrecordID - 1;
            self.newRecord.ID(NextNewrecordID);

            return true;
        };

        self.removeSelected = function (record) {
            if (record != null) {
                record.IsDeleted(true);
                //if(record.Code().length < 1)   record.Code("(Deleted)")
                if (record.IsAdded()) {
                    self.allRecords.remove(record);
                    //record.IsAdded(false);
                }
            }
        };

        self.unRemoveSelected = function (record) {
            if (record != null) // Prevent blanks and duplicates
            {
                record.IsDeleted(false);
            }
        };

        self.saveToServer = function () {
            var jsonData = ko.mapping.toJSON({ "changes": ko.mapping.toJS(self.allRecords) });
            $("input[type=button]").attr('disabled', 'disabled');
            showDlg(true);
            $.ajax({ // SO: 12846689
                url: location.href,
                type: "POST",
                dataType: "json",
                //processData: false,
                contentType: "application/json; charset=utf-8",
                data: jsonData,
                success: function (data) {
                    if (data != null && data.length > 0) {
                        showDlg(false);
                        alert(data);
                        $("input[type=button]").removeAttr('disabled');
                    }
                    else {
                        //alert("Operation was successful"); 
                        window.location.href = window.location.href; // Refresh page
                    }
                }
            });
            // ko.utils.postJson(location.href, jsonData ); //{ records: ko.mapping.toJS(self.allRecords) });
            return false;
        }
    };
    var vmMaster = new recordsViewModel();
    function createRecordsKO() {
        showDlg(true);
        $.getJSON(rolesKOVMURL, function (dataSet) {
            showDlg(false);
            var data = dataSet.records;
            var newRec;
            if (data != null && data.length > 0)
            { newRec = data.pop(); newRec.IsAdded = true; }

            vmMaster.allRecords = ko.mapping.fromJS(data); // Make sure the last record is removed
            vmMaster.newRecord = ko.mapping.fromJS(newRec);

            vmMaster.OrgTypes = ko.mapping.fromJS(dataSet.OrgTypes);
            ko.applyBindings(vmMaster);

            setFocus("btnAddNew");
        });
    }

    $(document).ready(function () {
        createRecordsKO();
        //setFocus("record1");
    });
