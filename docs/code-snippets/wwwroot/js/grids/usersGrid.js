/**
 * Users Grid Configuration
 */
var UsersGrid = (function() {
    "use strict";
    
    /**
     * Initializes the users grid
     * @param {string} gridId - Grid element selector (e.g., "#usersGrid")
     * @param {string} pagerId - Pager element selector (e.g., "#usersGridPager")
     */
    function initialize(gridId, pagerId) {
        var state = { lastPageSize: 10 };
        var apiEndpoint = "/api/user";
        
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
                    label: "Username", 
                    name: "username", 
                    width: 300,
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['cn', 'eq', 'ne', 'bw', 'ew']
                    },
                    editable: true,
                    editrules: { required: true },
                    editoptions: { size: 50, maxlength: 255 }
                }
            ],
            pager: pagerId,
            sortname: "id",
            caption: "Username",
            editurl: apiEndpoint,
            loadError: GridUtils.createLoadErrorHandler("users"),
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
            username: postData.username
        });
    }
    
    // Public API
    return {
        init: initialize
    };
})();