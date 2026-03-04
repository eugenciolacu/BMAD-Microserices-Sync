/**
 * Cells Grid Configuration
 */
var CellsGrid = (function() {
    "use strict";
    
    /**
     * Initializes the cells grid
     * @param {string} gridId - Grid element selector (e.g., "#cellsGrid")
     * @param {string} pagerId - Pager element selector (e.g., "#cellsGridPager")
     */
    function initialize(gridId, pagerId) {
        var state = { lastPageSize: 10 };
        var apiEndpoint = "/api/cell";
        
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
                    label: "Identifier", 
                    name: "identifier", 
                    width: 250,
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['cn', 'eq', 'ne', 'bw', 'ew']
                    },
                    editable: true,
                    editrules: { required: true },
                    editoptions: { size: 50, maxlength: 255 }
                },
                { 
                    label: "Surface ID", 
                    name: "surfaceId", 
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
            caption: "Cells",
            editurl: apiEndpoint,
            loadError: GridUtils.createLoadErrorHandler("cells"),
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
            identifier: postData.identifier,
            surfaceId: parseInt(postData.surfaceId, 10)
        });
    }
    
    // Public API
    return {
        init: initialize
    };
})();