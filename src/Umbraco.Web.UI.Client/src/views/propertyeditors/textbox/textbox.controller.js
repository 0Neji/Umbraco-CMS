function textboxController($rootScope, $scope, $log) {
    $scope.model.maxlength = false;
    if ($scope.model.config && $scope.model.config.maxChars) {
        $scope.model.maxlength = true;
        if($scope.model.value == undefined) {
            $scope.model.count = ($scope.model.config.maxChars * 1);
        } else {
            $scope.model.count = ($scope.model.config.maxChars * 1) - $scope.model.value.length;
        }
    }

    $scope.model.change = function() {
        if ($scope.model.config && $scope.model.config.maxChars) {
            if($scope.model.value == undefined) {
                $scope.model.count = ($scope.model.config.maxChars * 1);
            } else {
                $scope.model.count = ($scope.model.config.maxChars * 1) - $scope.model.value.length;
            }
            if($scope.model.count < 0) {
                $scope.model.value = $scope.model.value.substring(0, ($scope.model.config.maxChars * 1));
                $scope.model.count = 0;
            }
        }
    }
}
angular.module('umbraco').controller("Umbraco.PropertyEditors.textboxController", textboxController);