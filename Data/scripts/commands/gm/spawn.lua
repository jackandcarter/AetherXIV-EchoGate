require("global");

properties = {
    permissions = 0,
    parameters = "ddd",
    description = "Spawns an actor class with its own real appearance",
}

function onTrigger(player, argc, actorClassId, width, height)

	if (actorClassId == nil) then
		player:SendMessage(0x20, "", "No actor class id provided.");
		return;
	end	

    local pos = player:GetPos();
    local x = pos[0];
    local y = pos[1];
    local z = pos[2];
    local rot = pos[3];
    local zone = pos[4];
         
	actorClassId = tonumber(actorClassId);
	
	if (actorClassId ~= nil) then		
		zone = player:GetZone();
		local actor = nil;
		local w = tonumber(width) or 0;
        local h = tonumber(height) or 0;
        printf("%f %f %f", x, y, z);
        --local x, y, z = player.GetPos();
        for i = 0, w do
            for j = 0, h do
				local uniqueId = string.format("gm_spawn_%d_%d_%d_%d", actorClassId, i, j, math.random(1000000, 9999999));
				actor = zone:SpawnActor(actorClassId, uniqueId, pos[0] + ((i - (w / 2)) * 3), pos[1], pos[2] + ((j - (h / 2)) * 3), pos[3], 0, 0, true);
			end
		end

		if (actor == nil) then
			player:SendMessage(0x20, "", "This actor class id cannot be spawned.");
		end
	end
end;
