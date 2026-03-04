/**
 * Measurements Grid Configuration
 */
var MeasurementsGrid = (function() {
    "use strict";
    
    /**
     * Initializes the measurements grid
     * @param {string} gridId - Grid element selector (e.g., "#measurementsGrid")
     * @param {string} pagerId - Pager element selector (e.g., "#measurementsGridPager")
     */
    function initialize(gridId, pagerId) {
        var state = { lastPageSize: 10 };
        var apiEndpoint = "/api/measurement";
        
        // Get default options and merge with grid-specific options
        var gridOptions = $.extend({}, GridUtils.getDefaultGridOptions(), {
            url: apiEndpoint + "/paged",
            colModel: [
                {
                    label: "ID",
                    name: "id",
                    key: true,
                    width: 75,
                    align: "center",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                    },
                    editable: false
                },
                { 
                    label: "Alpha", 
                    name: "alpha", 
                    width: 100,
                    align: "right",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                    },
                    editable: true,
                    editrules: { required: true, number: true },
                    editoptions: { size: 10 },
                    formatter: "number",
                    formatoptions: { decimalPlaces: 2, thousandsSeparator: "" }
                },
                { 
                    label: "Beta", 
                    name: "beta", 
                    width: 100,
                    align: "right",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                    },
                    editable: true,
                    editrules: { required: true, number: true },
                    editoptions: { size: 10 },
                    formatter: "number",
                    formatoptions: { decimalPlaces: 2, thousandsSeparator: "" }
                },
                { 
                    label: "Offset X", 
                    name: "offsetX", 
                    width: 100,
                    align: "right",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                    },
                    editable: true,
                    editrules: { required: true, number: true },
                    editoptions: { size: 10 },
                    formatter: "number",
                    formatoptions: { decimalPlaces: 2, thousandsSeparator: "" }
                },
                { 
                    label: "Offset Y", 
                    name: "offsetY", 
                    width: 100,
                    align: "right",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                    },
                    editable: true,
                    editrules: { required: true, number: true },
                    editoptions: { size: 10 },
                    formatter: "number",
                    formatoptions: { decimalPlaces: 2, thousandsSeparator: "" }
                },
                { 
                    label: "Cell ID", 
                    name: "cellId", 
                    width: 75,
                    align: "center",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                    },
                    editable: true,
                    editrules: { required: true, integer: true, minValue: 1 },
                    editoptions: { size: 10 }
                },
                { 
                    label: "User ID", 
                    name: "userId", 
                    width: 75,
                    align: "center",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                    },
                    editable: true,
                    editrules: { required: true, integer: true, minValue: 1 },
                    editoptions: { size: 10 }
                }
            ],
            pager: pagerId,
            sortname: "id",
            caption: "Measurements",
            editurl: apiEndpoint,
            loadError: GridUtils.createLoadErrorHandler("measurements"),
            serializeGridData: GridUtils.createSerializeGridData(gridId, state)
        });
        
        // Initialize grid
        $(gridId).jqGrid(gridOptions);
        
        // Enable filter toolbar
        GridUtils.enableFilterToolbar(gridId);
        
        // Setup navigation with CRUD buttons
        GridUtils.setupNavigation(
            gridId, 
            pagerId, 
            apiEndpoint, 
            serializeEditData
        );
    }
    
    /**
     * Serializes data for add/edit operations
     * @param {object} postData - Form data
     * @returns {string} JSON string for API
     */
    function serializeEditData(postData) {
        return JSON.stringify({
            alpha: parseFloat(postData.alpha),
            beta: parseFloat(postData.beta),
            offsetX: parseFloat(postData.offsetX),
            offsetY: parseFloat(postData.offsetY),
            cellId: parseInt(postData.cellId, 10),
            userId: parseInt(postData.userId, 10)
        });
    }
    
    // Public API
    return {
        init: initialize
    };
})();