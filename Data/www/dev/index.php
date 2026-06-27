<?php

require_once(__DIR__ . "/../launcher/database.php");

function h($value)
{
	return htmlspecialchars((string)$value, ENT_QUOTES, "UTF-8");
}

function scalar_query($db, $sql)
{
	$result = $db->query($sql);
	if($result === false) return 0;
	$row = $result->fetch_row();
	return intval($row[0] ?? 0);
}

function table_exists($db, $table)
{
	if(!preg_match("/^[A-Za-z0-9_]+$/", $table)) return false;
	$escaped = $db->real_escape_string($table);
	$result = $db->query("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '$escaped'");
	if($result === false) return false;
	$row = $result->fetch_row();
	return intval($row[0] ?? 0) > 0;
}

function base_script_status($classPath)
{
	if($classPath === null || trim($classPath) === "") return array("status" => "blocked", "label" => "Blocked: blank classPath", "path" => "");

	$lastSlash = strrpos($classPath, "/");
	if($lastSlash === false) return array("status" => "missing", "label" => "Invalid classPath", "path" => "");

	$dir = strtolower(substr($classPath, 0, $lastSlash));
	$className = substr($classPath, $lastSlash + 1);
	$relative = "Data/scripts/base" . $dir . "/" . $className . ".lua";
	$root = realpath(__DIR__ . "/../../..");
	$absolute = $root . "/" . $relative;

	if(file_exists($absolute)) return array("status" => "ok", "label" => "Base Lua present", "path" => $relative);
	return array("status" => "missing", "label" => "Base Lua missing", "path" => $relative);
}

function readiness($row, $script)
{
	$hasClassPath = trim($row["classPath"] ?? "") !== "";
	$hasAppearance = $row["base"] !== null;
	$hasPool = $row["poolId"] !== null;
	$hasGroup = intval($row["groupCount"] ?? 0) > 0;
	$hasSpawn = intval($row["spawnCount"] ?? 0) > 0;

	if($hasClassPath && $hasAppearance && $script["status"] === "ok" && $hasPool && $hasGroup && $hasSpawn)
		return array("ok", "Restorable");
	if($hasAppearance && !$hasClassPath)
		return array("blocked", "Appearance-only");
	if($hasClassPath && $hasAppearance && $script["status"] !== "ok")
		return array("warn", "Needs base Lua");
	if($hasClassPath && $hasAppearance)
		return array("warn", "Needs spawn data");
	return array("blocked", "Incomplete");
}

function badge($status, $label)
{
	return "<span class=\"badge badge-" . h($status) . "\">" . h($label) . "</span>";
}

function trace_files()
{
	$files = glob("/tmp/aetherxiv-traces/*.jsonl");
	if($files === false) return array();
	usort($files, function($a, $b) { return filemtime($b) <=> filemtime($a); });
	return array_slice($files, 0, 12);
}

function latest_trace_hits($path)
{
	$lines = @file($path, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
	if($lines === false) return array();
	$lines = array_slice($lines, -250);
	$hits = array();
	foreach($lines as $line)
	{
		if(stripos($line, "missing") !== false || stripos($line, "resolved\":false") !== false || stripos($line, "error") !== false || stripos($line, "cannot") !== false)
		{
			$hits[] = $line;
			if(count($hits) >= 5) break;
		}
	}
	return $hits;
}

function clipped($value, $length)
{
	return substr(trim((string)$value), 0, $length);
}

$db = null;
$error = "";
$message = "";
try
{
	$db = launcher_database();
}
catch(Exception $e)
{
	$error = $e->getMessage();
}

$query = trim($_GET["q"] ?? "");
$zone = trim($_GET["zone"] ?? "");
$candidates = array();
$pins = array();
$stats = array();
$hasAppearanceAudit = false;

if($db !== null)
{
	$hasAppearanceAudit = table_exists($db, "server_battlenpc_appearance_audit");

	if($_SERVER["REQUEST_METHOD"] === "POST")
	{
		$action = $_POST["action"] ?? "";
		if($action === "appearance_audit")
		{
			if(!$hasAppearanceAudit)
			{
				$error = "Appearance audit table is missing. Run the database migrations before recording visual review state.";
			}
			else
			{
				$allowed = array("match", "wrong", "unsafe", "unsure");
				$appearanceId = intval($_POST["appearanceId"] ?? 0);
				$visualStatus = $_POST["visualStatus"] ?? "unsure";
				if($appearanceId > 0 && in_array($visualStatus, $allowed, true))
				{
					$expectedName = clipped($_POST["expectedName"] ?? "", 64);
					$sourceNote = clipped($_POST["sourceNote"] ?? "", 255);
					$notes = clipped($_POST["notes"] ?? "", 255);
					$updatedBy = "dev-portal";
					$stmt = $db->prepare("
						INSERT INTO server_battlenpc_appearance_audit
							(appearanceId, expectedName, visualStatus, sourceNote, notes, updatedBy)
						VALUES (?, ?, ?, ?, ?, ?)
						ON DUPLICATE KEY UPDATE
							expectedName = VALUES(expectedName),
							visualStatus = VALUES(visualStatus),
							sourceNote = VALUES(sourceNote),
							notes = VALUES(notes),
							updatedBy = VALUES(updatedBy)");
					if($stmt !== false)
					{
						$stmt->bind_param("isssss", $appearanceId, $expectedName, $visualStatus, $sourceNote, $notes, $updatedBy);
						$stmt->execute();
						$stmt->close();
						$redirectParams = array();
						if(trim($_POST["q"] ?? "") !== "") $redirectParams["q"] = trim($_POST["q"]);
						if(trim($_POST["zone"] ?? "") !== "") $redirectParams["zone"] = trim($_POST["zone"]);
						$location = "/dev/";
						if(count($redirectParams) > 0) $location .= "?" . http_build_query($redirectParams);
						header("Location: " . $location);
						exit;
					}
					else
					{
						$error = "Could not prepare appearance audit update.";
					}
				}
				else
				{
					$error = "Invalid appearance audit update.";
				}
			}
		}
	}

	$stats = array(
		"actor_classes" => scalar_query($db, "SELECT COUNT(*) FROM gamedata_actor_class"),
		"spawnable_classes" => scalar_query($db, "SELECT COUNT(*) FROM gamedata_actor_class WHERE classPath <> ''"),
		"appearances" => scalar_query($db, "SELECT COUNT(*) FROM gamedata_actor_appearance"),
		"pools" => table_exists($db, "server_battlenpc_pools") ? scalar_query($db, "SELECT COUNT(*) FROM server_battlenpc_pools") : 0,
		"groups" => table_exists($db, "server_battlenpc_groups") ? scalar_query($db, "SELECT COUNT(*) FROM server_battlenpc_groups") : 0,
		"spawns" => table_exists($db, "server_battlenpc_spawn_locations") ? scalar_query($db, "SELECT COUNT(*) FROM server_battlenpc_spawn_locations") : 0,
		"pins" => table_exists($db, "server_battlenpc_spawn_audit_pins") ? scalar_query($db, "SELECT COUNT(*) FROM server_battlenpc_spawn_audit_pins") : 0,
		"appearance_audits" => $hasAppearanceAudit ? scalar_query($db, "SELECT COUNT(*) FROM server_battlenpc_appearance_audit") : 0,
	);

	$where = array();
	$params = array();
	$types = "";

	if($query !== "")
	{
		if(ctype_digit($query))
		{
			$where[] = "(ac.id = ? OR ac.displayNameId = ? OR aa.base = ? OR p.poolId = ? OR p.genusId = ?)";
			for($i = 0; $i < 5; $i++)
			{
				$params[] = intval($query);
				$types .= "i";
			}
		}
		else
		{
			$where[] = "(ac.classPath LIKE ? OR p.name LIKE ? OR g.name LIKE ?)";
			$like = "%" . $query . "%";
			$params[] = $like;
			$params[] = $like;
			$params[] = $like;
			$types .= "sss";
		}
	}

	if($zone !== "" && ctype_digit($zone))
	{
		$where[] = "EXISTS (SELECT 1 FROM server_battlenpc_groups zg WHERE zg.poolId = p.poolId AND zg.zoneId = ?)";
		$params[] = intval($zone);
		$types .= "i";
	}

	$auditSelect = $hasAppearanceAudit
		? "audit.visualStatus, audit.expectedName, audit.sourceNote AS auditSourceNote, audit.notes AS auditNotes, audit.updatedAt AS auditUpdatedAt,"
		: "NULL AS visualStatus, NULL AS expectedName, NULL AS auditSourceNote, NULL AS auditNotes, NULL AS auditUpdatedAt,";
	$auditJoin = $hasAppearanceAudit
		? "LEFT JOIN server_battlenpc_appearance_audit audit ON audit.appearanceId = ac.id"
		: "";
	$whereSql = count($where) > 0 ? "WHERE " . implode(" AND ", $where) : "";
	$sql = "
		SELECT
			ac.id AS actorClassId,
			ac.classPath,
			ac.displayNameId,
			aa.base,
			aa.size,
			$auditSelect
			p.poolId,
			p.name AS poolName,
			p.genusId,
			g.name AS genusName,
			(SELECT COUNT(*) FROM server_battlenpc_groups bg WHERE bg.poolId = p.poolId) AS groupCount,
			(SELECT COUNT(*) FROM server_battlenpc_spawn_locations sl INNER JOIN server_battlenpc_groups sg ON sg.groupId = sl.groupId WHERE sg.poolId = p.poolId) AS spawnCount,
			(SELECT GROUP_CONCAT(DISTINCT zoneId ORDER BY zoneId SEPARATOR ', ') FROM server_battlenpc_groups zg WHERE zg.poolId = p.poolId) AS zones
		FROM gamedata_actor_class ac
		LEFT JOIN gamedata_actor_appearance aa ON aa.id = ac.id
		LEFT JOIN server_battlenpc_pools p ON p.actorClassId = ac.id
		LEFT JOIN server_battlenpc_genus g ON g.genusId = p.genusId
		$auditJoin
		$whereSql
		ORDER BY
			CASE WHEN p.poolId IS NULL THEN 1 ELSE 0 END ASC,
			CASE WHEN ac.classPath = '' THEN 1 ELSE 0 END ASC,
			ac.id ASC
		LIMIT 300";

	$statement = $db->prepare($sql);
	if($statement !== false)
	{
		if($types !== "") $statement->bind_param($types, ...$params);
		$statement->execute();
		$result = $statement->get_result();
		while($row = $result->fetch_assoc()) $candidates[] = $row;
		$statement->close();
	}

	if(table_exists($db, "server_battlenpc_spawn_audit_pins"))
	{
		$pinSql = "
			SELECT enemyName, sourceNote, zoneId, COUNT(*) AS pinCount,
				AVG(positionX) AS avgX, AVG(positionY) AS avgY, AVG(positionZ) AS avgZ,
				SUM(CASE WHEN isPromoted = 1 THEN 1 ELSE 0 END) AS promotedCount,
				MAX(createdAt) AS latestPin
			FROM server_battlenpc_spawn_audit_pins
			GROUP BY enemyName, sourceNote, zoneId
			ORDER BY latestPin DESC
			LIMIT 80";
		$result = $db->query($pinSql);
		if($result !== false)
		{
			while($row = $result->fetch_assoc()) $pins[] = $row;
		}
	}
}

?>
<!doctype html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title>Aether Dev Portal</title>
	<style>
		:root {
			color-scheme: light;
			--bg: #f6f7f9;
			--panel: #ffffff;
			--line: #d8dee8;
			--text: #19202a;
			--muted: #667085;
			--accent: #0f766e;
			--warn: #a16207;
			--bad: #b42318;
			--ok-bg: #e7f7f0;
			--warn-bg: #fff6d9;
			--bad-bg: #ffe7e4;
			--soft: #eef2f6;
		}
		* { box-sizing: border-box; }
		body {
			margin: 0;
			font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
			background: var(--bg);
			color: var(--text);
			font-size: 14px;
		}
		header {
			background: #17202b;
			color: white;
			padding: 18px 24px;
			border-bottom: 4px solid var(--accent);
		}
		header h1 { margin: 0; font-size: 22px; font-weight: 650; }
		header p { margin: 4px 0 0; color: #cbd5e1; }
		main { padding: 20px 24px 40px; max-width: 1600px; margin: 0 auto; }
		.grid { display: grid; gap: 14px; }
		.stats { grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); margin-bottom: 18px; }
		.card {
			background: var(--panel);
			border: 1px solid var(--line);
			border-radius: 6px;
			padding: 14px;
		}
		.stat .label { color: var(--muted); font-size: 12px; text-transform: uppercase; letter-spacing: .02em; }
		.stat .value { font-size: 28px; font-weight: 700; margin-top: 4px; }
		h2 { font-size: 18px; margin: 0 0 12px; }
		h3 { font-size: 15px; margin: 0 0 10px; }
		form.filters { display: flex; gap: 10px; align-items: end; flex-wrap: wrap; margin-bottom: 12px; }
		label { display: grid; gap: 4px; color: var(--muted); font-size: 12px; }
		input {
			min-width: 220px;
			padding: 8px 10px;
			border: 1px solid var(--line);
			border-radius: 4px;
			font-size: 14px;
		}
		button, .button {
			border: 1px solid #0c5d57;
			background: var(--accent);
			color: white;
			border-radius: 4px;
			padding: 9px 12px;
			text-decoration: none;
			font-weight: 600;
			cursor: pointer;
		}
		table { width: 100%; border-collapse: collapse; }
		th, td { border-bottom: 1px solid var(--line); padding: 8px 9px; text-align: left; vertical-align: top; }
		th { color: var(--muted); font-size: 12px; background: #fafbfc; position: sticky; top: 0; z-index: 1; }
		.scroll { overflow: auto; max-height: 640px; border: 1px solid var(--line); border-radius: 6px; }
		code {
			background: var(--soft);
			border: 1px solid var(--line);
			border-radius: 4px;
			padding: 2px 5px;
			white-space: nowrap;
		}
		.path { font-size: 12px; color: var(--muted); overflow-wrap: anywhere; }
		.badge { display: inline-block; padding: 2px 7px; border-radius: 999px; font-size: 12px; font-weight: 650; }
		.badge-ok { background: var(--ok-bg); color: #067647; }
		.badge-warn { background: var(--warn-bg); color: var(--warn); }
		.badge-blocked, .badge-missing { background: var(--bad-bg); color: var(--bad); }
		.badge-soft { background: var(--soft); color: var(--muted); }
		.badge-match { background: var(--ok-bg); color: #067647; }
		.badge-wrong, .badge-unsafe { background: var(--bad-bg); color: var(--bad); }
		.badge-unsure { background: var(--warn-bg); color: var(--warn); }
		.audit-actions { display: flex; gap: 5px; flex-wrap: wrap; margin-top: 8px; max-width: 360px; }
		.audit-actions input { min-width: 160px; flex: 1 1 160px; padding: 6px 7px; font-size: 12px; }
		.audit-actions button { padding: 6px 8px; font-size: 12px; }
		.button-small { display: inline-block; padding: 4px 6px; margin: 2px 0; font-size: 12px; }
		.two { grid-template-columns: minmax(0, 1fr) minmax(360px, .42fr); align-items: start; }
		@media (max-width: 1000px) { .two { grid-template-columns: 1fr; } }
		.trace-line {
			background: #101828;
			color: #d0d5dd;
			border-radius: 4px;
			padding: 8px;
			overflow: auto;
			font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
			font-size: 12px;
			margin: 6px 0;
		}
		.note { color: var(--muted); line-height: 1.45; }
		.error { color: var(--bad); background: var(--bad-bg); border: 1px solid #fecdca; padding: 10px; border-radius: 6px; }
	</style>
</head>
<body>
	<header>
		<h1>Aether Dev Portal</h1>
		<p>Local reverse-engineering and enemy restoration workbench</p>
	</header>
	<main>
		<?php if($error !== "") { ?>
			<div class="error">Portal warning: <?php echo h($error); ?></div>
		<?php } ?>
		<?php if($message !== "") { ?>
			<div class="card"><?php echo h($message); ?></div>
		<?php } ?>

		<section class="grid stats">
			<?php foreach($stats as $label => $value) { ?>
				<div class="card stat">
					<div class="label"><?php echo h(str_replace("_", " ", $label)); ?></div>
					<div class="value"><?php echo h($value); ?></div>
				</div>
			<?php } ?>
		</section>

		<section class="grid two">
			<div class="card">
				<h2>Enemy Readiness</h2>
				<form class="filters" method="get">
					<label>Search actor, path, pool, genus, model base
						<input name="q" value="<?php echo h($query); ?>" placeholder="2101001, Ant, Lemming, 10012">
					</label>
					<label>Zone
						<input name="zone" value="<?php echo h($zone); ?>" placeholder="170">
					</label>
					<button type="submit">Audit</button>
					<a class="button" href="/dev/">Reset</a>
				</form>
				<div class="scroll">
					<table>
						<thead>
							<tr>
								<th>Status</th>
								<th>Actor</th>
								<th>Appearance</th>
								<th>Pool</th>
								<th>Script</th>
								<th>Preview</th>
							</tr>
						</thead>
						<tbody>
							<?php foreach($candidates as $row) {
								$script = base_script_status($row["classPath"]);
								$ready = readiness($row, $script);
								$actorId = intval($row["actorClassId"]);
								$visualStatus = $row["visualStatus"] ?? "";
								$visualLabel = $visualStatus !== "" && $visualStatus !== null ? ucfirst($visualStatus) : "Not reviewed";
							?>
							<tr>
								<td><?php echo badge($ready[0], $ready[1]); ?></td>
								<td>
									<strong><?php echo h($actorId); ?></strong><br>
									<span class="path"><?php echo h($row["classPath"] !== "" ? $row["classPath"] : "blank classPath"); ?></span><br>
									<span class="path">displayNameId <?php echo h($row["displayNameId"]); ?></span>
								</td>
								<td>
									<?php if($row["base"] !== null) { ?>
										base <code><?php echo h($row["base"]); ?></code><br>
										size <code><?php echo h($row["size"]); ?></code>
									<?php } else { ?>
										<?php echo badge("blocked", "missing"); ?>
									<?php } ?>
								</td>
								<td>
									<?php if($row["poolId"] !== null) { ?>
										<strong><?php echo h($row["poolName"]); ?></strong><br>
										pool <code><?php echo h($row["poolId"]); ?></code> genus <code><?php echo h($row["genusId"]); ?></code><br>
										<span class="path"><?php echo h($row["genusName"]); ?>, groups <?php echo h($row["groupCount"]); ?>, spawns <?php echo h($row["spawnCount"]); ?>, zones <?php echo h($row["zones"] ?? ""); ?></span>
									<?php } else { ?>
										<?php echo badge("soft", "no pool"); ?>
									<?php } ?>
								</td>
								<td>
									<?php echo badge($script["status"] === "ok" ? "ok" : ($script["status"] === "missing" ? "warn" : "blocked"), $script["label"]); ?><br>
									<?php if($script["path"] !== "") { ?><span class="path"><?php echo h($script["path"]); ?></span><?php } ?>
								</td>
								<td>
									<?php if($row["base"] !== null) { ?>
										<code>!previewappearance <?php echo h($actorId); ?></code><br>
										<code>!previewpair <?php echo h($actorId); ?> <?php echo h($actorId); ?></code><br>
									<?php } ?>
									<?php if(trim($row["classPath"] ?? "") !== "") { ?><code>!spawn <?php echo h($actorId); ?></code><?php } ?>
									<div style="margin-top: 8px;">
										<?php echo badge($visualStatus !== "" && $visualStatus !== null ? $visualStatus : "soft", $visualLabel); ?>
										<?php if($row["auditUpdatedAt"] !== null) { ?><span class="path"><?php echo h($row["auditUpdatedAt"]); ?></span><?php } ?>
									</div>
									<?php if($hasAppearanceAudit && $row["base"] !== null) { ?>
										<form class="audit-actions" method="post">
											<input type="hidden" name="action" value="appearance_audit">
											<input type="hidden" name="appearanceId" value="<?php echo h($actorId); ?>">
											<input type="hidden" name="q" value="<?php echo h($query); ?>">
											<input type="hidden" name="zone" value="<?php echo h($zone); ?>">
											<input name="expectedName" value="<?php echo h($row["expectedName"] ?? ""); ?>" placeholder="expected name">
											<input name="sourceNote" value="<?php echo h($row["auditSourceNote"] ?? ""); ?>" placeholder="source note">
											<input name="notes" value="<?php echo h($row["auditNotes"] ?? ""); ?>" placeholder="notes">
											<button name="visualStatus" value="match" type="submit">Match</button>
											<button name="visualStatus" value="wrong" type="submit">Wrong</button>
											<button name="visualStatus" value="unsafe" type="submit">Unsafe</button>
											<button name="visualStatus" value="unsure" type="submit">Unsure</button>
										</form>
									<?php } ?>
								</td>
							</tr>
							<?php } ?>
						</tbody>
					</table>
				</div>
			</div>

			<div class="grid">
				<div class="card">
					<h2>Safe Preview Loop</h2>
					<p class="note">Use appearance preview for incomplete rows. It spawns a known-good shell actor and applies the candidate appearance, avoiding unsafe fake class paths.</p>
					<p><code>!previewappearance 2101001</code></p>
					<p><code>!previewrange 2101001 11</code></p>
					<p><code>!previewclear 2101001 11</code></p>
					<p class="note">Use pair preview to compare the real server actor beside the safe-shell appearance candidate. The server side may fail when class data is still incomplete; the preview side should still tell you what the appearance renders as.</p>
					<p><code>!previewpair 2104002 2104002</code></p>
					<p class="note">Use actor-class spawn only when the row has a nonblank, verified class path.</p>
					<p><code>!spawn 2104002</code></p>
				</div>

				<div class="card">
					<h2>Pin Groups</h2>
					<div class="scroll" style="max-height: 320px;">
						<table>
							<thead><tr><th>Enemy</th><th>Zone</th><th>Pins</th><th>Average Position</th></tr></thead>
							<tbody>
								<?php foreach($pins as $pin) { ?>
									<tr>
										<td><strong><?php echo h($pin["enemyName"]); ?></strong><br><span class="path"><?php echo h($pin["sourceNote"]); ?></span></td>
										<td><?php echo h($pin["zoneId"]); ?></td>
										<td><?php echo h($pin["pinCount"]); ?><?php if(intval($pin["promotedCount"]) > 0) echo " / " . h($pin["promotedCount"]) . " promoted"; ?></td>
										<td><code><?php echo h(number_format(floatval($pin["avgX"]), 3)); ?>, <?php echo h(number_format(floatval($pin["avgY"]), 3)); ?>, <?php echo h(number_format(floatval($pin["avgZ"]), 3)); ?></code></td>
									</tr>
								<?php } ?>
							</tbody>
						</table>
					</div>
				</div>

				<div class="card">
					<h2>Trace Files</h2>
					<?php foreach(trace_files() as $file) { ?>
						<h3><?php echo h(basename($file)); ?></h3>
						<p class="path"><?php echo h($file); ?>, <?php echo h(number_format(filesize($file) / 1024, 1)); ?> KB</p>
						<?php foreach(latest_trace_hits($file) as $hit) { ?>
							<div class="trace-line"><?php echo h($hit); ?></div>
						<?php } ?>
					<?php } ?>
				</div>
			</div>
		</section>
	</main>
</body>
</html>
