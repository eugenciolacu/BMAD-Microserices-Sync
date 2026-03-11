/**
 * Shared utilities for jqGrid initialization and management
 */
var GridUtils = (function() {
    "use strict";
    
    /**
     * Adds keyboard handlers (Enter to submit, ESC to cancel) to grid dialogs
     * @param {string} dialogSelector - jQuery selector for the dialog
     * @param {boolean} isEditOrAdd - true for add/edit dialogs, false for delete dialog
     */
    function addKeyboardHandlers(dialogSelector, isEditOrAdd) {
        var $dialog = $(dialogSelector);
        
        $dialog.off("keydown").on("keydown", function(e) {
            // Enter key - submit
            if (e.keyCode === 13) {
                // Don't submit if typing in textarea for add/edit dialogs
                if (isEditOrAdd && e.target.tagName === "TEXTAREA") {
                    return;
                }
                e.preventDefault();
                var submitBtn = isEditOrAdd ? "#sData" : "#dData";
                $(submitBtn, this).trigger("click");
                return false;
            }
            
            // ESC key - cancel
            if (e.keyCode === 27) {
                e.preventDefault();
                var cancelBtn = isEditOrAdd ? "#cData" : "#eData";
                $(cancelBtn).trigger("click");
                return false;
            }
        });
    }
    
    /**
     * Centers a dialog in the viewport
     * @param {string} dialogSelector - jQuery selector for the dialog
     */
    function centerDialog(dialogSelector) {
        var $dialog = $(dialogSelector);
        if ($dialog.length === 0) {
            return;
        }
        
        var $window = $(window);
        var windowHeight = $window.height();
        var windowWidth = $window.width();
        var scrollTop = $window.scrollTop();
        var scrollLeft = $window.scrollLeft();
        
        var dialogHeight = $dialog.outerHeight();
        var dialogWidth = $dialog.outerWidth();
        
        var top = scrollTop + (windowHeight - dialogHeight) / 2;
        var left = scrollLeft + (windowWidth - dialogWidth) / 2;
        
        // Ensure dialog doesn't go off screen
        top = Math.max(scrollTop + 10, top);
        left = Math.max(scrollLeft + 10, left);
        
        $dialog.css({
            top: top + "px",
            left: left + "px"
        });
    }
    
    /**
     * Standard response handler for add/edit/delete operations
     * @param {object} response - Ajax response object
     * @param {object} postData - Posted data
     * @returns {array} [success, message, newId] tuple expected by jqGrid
     */
    function handleSubmitResponse(response, postData) {
        if (response.status === 200 || response.status === 201 || response.status === 204) {
            return [true, "", ""];
        } else {
            var errorMsg = response.responseText;
            try {
                var errorObj = JSON.parse(response.responseText);
                errorMsg = errorObj.title || errorMsg;
            } catch(e) {
                // If parsing fails, use the raw response text
            }
            return [false, errorMsg, ""];
        }
    }
    
    /**
     * Creates edit options for navGrid
     * @param {string} gridId - Grid selector (e.g., "#measurementsGrid")
     * @param {string} apiEndpoint - API base path (e.g., "/api/v1/measurements-grid")
     * @param {function} serializeDataFn - Function to serialize edit data
     * @returns {object} Edit options configuration
     */
    function getEditOptions(gridId, apiEndpoint, serializeDataFn) {
        return {
            closeAfterEdit: true,
            reloadAfterSubmit: true,
            mtype: "PUT",
            serializeEditData: serializeDataFn,
            ajaxEditOptions: { 
                contentType: "application/json" 
            },
            beforeShowForm: function(form) {
                $("#id", form).prop("disabled", true);
            },
            afterShowForm: function(formId) {
                var dialogId = "#editmod" + $(gridId)[0].id;
                addKeyboardHandlers(dialogId, true);
                centerDialog(dialogId);
            },
            onclickSubmit: function(options, postData) {
                var rowid = $(gridId).jqGrid('getGridParam', 'selrow');
                options.url = apiEndpoint + "/" + rowid;
                return {};
            },
            afterSubmit: handleSubmitResponse
        };
    }
    
    /**
     * Creates add options for navGrid
     * @param {string} gridId - Grid selector (e.g., "#measurementsGrid")
     * @param {string} apiEndpoint - API base path (e.g., "/api/v1/measurements-grid")
     * @param {function} serializeDataFn - Function to serialize add data
     * @returns {object} Add options configuration
     */
    function getAddOptions(gridId, apiEndpoint, serializeDataFn) {
        return {
            closeAfterAdd: true,
            reloadAfterSubmit: true,
            mtype: "POST",
            serializeEditData: serializeDataFn,
            ajaxEditOptions: { 
                contentType: "application/json" 
            },
            beforeShowForm: function(form) {
                $("#tr_id", form).hide();
            },
            afterShowForm: function(formId) {
                var dialogId = "#editmod" + $(gridId)[0].id;
                addKeyboardHandlers(dialogId, true);
                centerDialog(dialogId);
            },
            onclickSubmit: function(options) {
                options.url = apiEndpoint;
                return {};
            },
            afterSubmit: handleSubmitResponse
        };
    }
    
    /**
     * Creates delete options for navGrid
     * @param {string} gridId - Grid selector (e.g., "#measurementsGrid")
     * @param {string} apiEndpoint - API base path (e.g., "/api/v1/measurements-grid")
     * @returns {object} Delete options configuration
     */
    function getDeleteOptions(gridId, apiEndpoint) {
        return {
            mtype: "DELETE",
            reloadAfterSubmit: true,
            afterShowForm: function(formId) {
                var dialogId = "#delmod" + $(gridId)[0].id;
                addKeyboardHandlers(dialogId, false);
                centerDialog(dialogId);
                // Set focus on Delete button for better UX
                setTimeout(function() {
                    $("#dData").focus();
                }, 100);
            },
            onclickSubmit: function(options, rowid) {
                options.url = apiEndpoint + "/" + rowid;
                return {};
            },
            afterSubmit: handleSubmitResponse
        };
    }
    
    /**
     * Gets default grid configuration options
     * @returns {object} Default grid options
     */
    function getDefaultGridOptions() {
        return {
            datatype: "json",
            mtype: "GET",
            rowNum: 10,
            rowList: [10, 20, 50],
            sortorder: "asc",
            viewrecords: true,
            height: "auto",
            autowidth: true,
            jsonReader: {
                root: "data",
                page: "page",
                total: "totalPages",
                records: "totalCount",
                repeatitems: false,
                id: "id"
            }
        };
    }
    
    /**
     * Creates standard serializeGridData function with page size tracking
     * @param {string} gridId - Grid selector
     * @param {object} state - Object to store lastPageSize state
     * @returns {function} serializeGridData function
     */
    function createSerializeGridData(gridId, state) {
        return function(postData) {
            // If page size changed, reset to page 1
            if (postData.rows !== state.lastPageSize) {
                state.lastPageSize = postData.rows;
                postData.page = 1;
                $(gridId).jqGrid("setGridParam", { page: 1 });
            }
            
            return {
                page: postData.page,
                pageSize: postData.rows,
                sortBy: postData.sidx,
                sortOrder: postData.sord,
                filters: postData.filters
            };
        };
    }
    
    /**
     * Creates standard loadError handler
     * @param {string} entityName - Name of entity for error messages (e.g., "measurements")
     * @returns {function} loadError handler
     */
    function createLoadErrorHandler(entityName) {
        return function(xhr, status, error) {
            console.error("Error loading " + entityName + " grid:", status, error);
            alert("Failed to load " + entityName + " data. Check console for details.");
        };
    }
    
    /**
     * Sets up navigation bar with CRUD buttons
     * @param {string} gridId - Grid selector
     * @param {string} pagerId - Pager selector
     * @param {string} apiEndpoint - API base path
     * @param {function} serializeDataFn - Function to serialize data for add/edit
     */
    function setupNavigation(gridId, pagerId, apiEndpoint, serializeDataFn) {
        $(gridId).jqGrid('navGrid', pagerId, {
            edit: true,
            add: true,
            del: true,
            search: false,
            refresh: true,
            view: false,
            position: "left",
            cloneToTop: false
        },
        getEditOptions(gridId, apiEndpoint, serializeDataFn),
        getAddOptions(gridId, apiEndpoint, serializeDataFn),
        getDeleteOptions(gridId, apiEndpoint)
        );
    }
    
    /**
     * Enables filter toolbar for grid
     * @param {string} gridId - Grid selector
     */
    function enableFilterToolbar(gridId) {
        $(gridId).jqGrid('filterToolbar', {
            searchOnEnter: false,
            stringResult: true,
            defaultSearch: 'cn'
        });
    }
    
    // Public API
    return {
        addKeyboardHandlers: addKeyboardHandlers,
        centerDialog: centerDialog,
        handleSubmitResponse: handleSubmitResponse,
        getEditOptions: getEditOptions,
        getAddOptions: getAddOptions,
        getDeleteOptions: getDeleteOptions,
        getDefaultGridOptions: getDefaultGridOptions,
        createSerializeGridData: createSerializeGridData,
        createLoadErrorHandler: createLoadErrorHandler,
        setupNavigation: setupNavigation,
        enableFilterToolbar: enableFilterToolbar
    };
})();
