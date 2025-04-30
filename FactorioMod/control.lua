script.on_nth_tick(10, function()
    for index, player in pairs(game.connected_players) do
        local comms = string.pack("ddJ", player.position.x, player.position.y, player.surface_index)
        helpers.write_file("fdpa-comm", comms, false, player.index)
    end
end)
