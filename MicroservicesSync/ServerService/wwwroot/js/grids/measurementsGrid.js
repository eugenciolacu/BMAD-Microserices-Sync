/**
 * Measurements Grid Configuration
 * Adapted from reference buildingsGrid.js pattern for Guid-based Measurement entity.
 */
var MeasurementsGrid = (function() {
    "use strict";

    function initialize(gridId, pagerId) {
        var state = { lastPageSize: 10 };
        var apiEndpoint = "/api/v1/measurements-grid";

        var gridOptions = $.extend({}, GridUtils.getDefaultGridOptions(), {
            url: apiEndpoint + "/paged",
            colModel: [
                {
                    label: "ID",
                    name: "id",
                    key: true,
                    width: 280,
                    align: "left",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne']
                    },
                    editable: false
                },
                {
                    label: "Value",
                    name: "value",
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
                    label: "Recorded At",
                    name: "recordedAt",
                    width: 160,
                    align: "center",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                    },
                    editable: true,
                    editrules: { required: true },
                    editoptions: { size: 20 }
                },
                {
                    label: "Synced At",
                    name: "syncedAt",
                    width: 160,
                    align: "center",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                    },
                    editable: false
                },
                {
                    label: "User ID",
                    name: "userId",
                    width: 280,
                    align: "left",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne']
                    },
                    editable: true,
                    editrules: { required: true },
                    editoptions: { size: 36 }
                },
                {
                    label: "Cell ID",
                    name: "cellId",
                    width: 280,
                    align: "left",
                    sortable: true,
                    search: true,
                    searchoptions: {
                        sopt: ['eq', 'ne']
                    },
                    editable: true,
                    editrules: { required: true },
                    editoptions: { size: 36 }
                }
            ],
            pager: pagerId,
            sortname: "recordedAt",
            sortorder: "desc",
            caption: "Measurements",
            editurl: apiEndpoint,
            loadError: GridUtils.createLoadErrorHandler("measurements"),
            serializeGridData: GridUtils.createSerializeGridData(gridId, state),
            shrinkToFit: true,
            autowidth: true
        });

        $(gridId).jqGrid(gridOptions);

        // Enable resizable columns
        $(gridId).jqGrid('setGridWidth', $(gridId).closest('.ui-jqgrid').parent().width(), true);
        $(window).on('resize', function() {
            $(gridId).jqGrid('setGridWidth', $(gridId).closest('.ui-jqgrid').parent().width(), true);
        });

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

    function serializeEditData(postData) {
        return JSON.stringify({
            value: parseFloat(postData.value),
            recordedAt: postData.recordedAt,
            userId: postData.userId,
            cellId: postData.cellId
        });
    }

    return {
        init: initialize
    };
})();
