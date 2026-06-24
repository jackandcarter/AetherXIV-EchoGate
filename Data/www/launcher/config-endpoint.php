<?php

require_once(__DIR__ . "/database.php");

try
{
	$db = launcher_database();
	$config = launcher_config_map($db);
	launcher_json(array(
		"service_version" => intval($config["service_version"] ?? "1"),
		"server_name" => $config["server_name"] ?? "MeteorXIV Core v1.2",
		"server_status_url" => "status",
		"news_url" => "news",
		"patch_manifest_url" => "patch-manifest",
		"runtime_catalog_url" => "runtime-catalog",
		"client_plugin_framework_catalog_url" => $config["client_plugin_framework_catalog_url"] ?? "umbra/framework-catalog",
		"plugin_catalog_urls" => launcher_config_list($config["plugin_catalog_urls"] ?? ""),
		"login_url" => $config["login_url"] ?? "login",
		"account_create_url" => $config["account_create_url"] ?? "create-account",
		"client_login_url" => $config["client_login_url"] ?? "../login/index.php",
		"patch_base_url" => $config["patch_base_url"] ?? "",
		"target_boot_version" => $config["target_boot_version"] ?? "2010.09.18.0000",
		"target_game_version" => $config["target_game_version"] ?? "2012.09.19.0001"
	));
}
catch(Exception $e)
{
	http_response_code(500);
	launcher_json(array("error" => "launcher config unavailable"));
}

?>
