<?php

require_once(__DIR__ . "/database.php");

try
{
	$db = launcher_database();
	$statement = $db->prepare("SELECT id, title, summary, body, banner_url, link_url, published_at FROM launcher_news WHERE is_published = 1 ORDER BY sort_order ASC, published_at DESC LIMIT 10");
	$statement->execute();
	$result = $statement->get_result();
	$items = array();
	while($row = $result->fetch_assoc())
	{
		$items[] = array(
			"id" => intval($row["id"]),
			"title" => $row["title"],
			"summary" => $row["summary"],
			"body" => $row["body"],
			"banner_url" => $row["banner_url"],
			"link_url" => $row["link_url"],
			"published_at" => gmdate("c", strtotime($row["published_at"]))
		);
	}
	launcher_json(array("items" => $items));
}
catch(Exception $e)
{
	http_response_code(500);
	launcher_json(array("items" => array()));
}

?>
