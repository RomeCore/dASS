--[[
name: ollama
title: Ollama models
description: Control panel for Ollama - lists the models currently loaded in memory with their size and expiry and unloads any of them with one click. Has a Refresh button. Talks to the local Ollama server on 127.0.0.1:11434.
category: Chat
generate: false
]]
-- Ollama control panel. The AXAML root MUST carry xmlns="https://github.com/avaloniaui",
-- or the dynamic control fails to resolve the element types at render time.

local BASE = "http://127.0.0.1:11434"

local function human(bytes)
	if type(bytes) ~= "number" then return "" end
	local gb = bytes / 1024 / 1024 / 1024
	if gb >= 1 then return string.format("%.2f GB", gb) end
	return string.format("%.0f MB", bytes / 1024 / 1024)
end

local function short_time(iso)
	if type(iso) ~= "string" then return "" end
	local hh, mm, ss = iso:match("T(%d%d):(%d%d):(%d%d)")
	if hh then return hh .. ":" .. mm .. ":" .. ss end
	return iso
end

local vm = {}
vm.status = "Loading..."
vm.models = {}
vm.emptyVisible = false

local refresh_fn

local async function fetch_models()
	local res = await web.request("GET", BASE .. "/api/ps")
	if res.status_code ~= 200 then
		return nil, string.format("%d %s", res.status_code, tostring(res.status_description))
	end
	local data = json.decode(res.content)
	return data.models or {}
end

local function build_items(models)
	local items = {}
	for _, m in ipairs(models) do
		local item = {}
		item.name = m.name
		item.detail = human(m.size) .. "  ·  expires " .. short_time(m.expires_at)
		item.unload = async function(self)
			local res = await web.request("POST", BASE .. "/api/generate", {
				content = { model = self.name, keep_alive = 0, stream = false },
				contentType = "application/json"
			})
			if res.status_code == 200 then
				dass.toasts.success("Unloaded " .. tostring(self.name))
			else
				dass.toasts.error("Failed to unload " .. tostring(self.name),
					string.format("%d %s", res.status_code, tostring(res.status_description)))
			end
			await refresh_fn()
		end
		items[#items + 1] = item
	end
	return items
end

refresh_fn = async function()
	local models, err = await fetch_models()
	if err then
		vm:set("status", "Ollama unreachable: " .. err)
		vm:set("models", {})
		vm:set("emptyVisible", true)
		return
	end
	local total = 0
	for _, m in ipairs(models) do total = total + (tonumber(m.size) or 0) end
	local summary
	if #models == 0 then
		summary = "No models loaded in memory."
	else
		summary = tostring(#models) .. (#models == 1 and " model · " or " models · ") .. human(total) .. " resident"
	end
	vm:set("status", summary)
	vm:set("models", build_items(models))
	vm:set("emptyVisible", #models == 0)
end

vm.refresh = async function(self) await refresh_fn() end

local xaml = [[
<Border xmlns="https://github.com/avaloniaui"
        Background="#0D1117" BorderBrush="#30363D" BorderThickness="1"
        CornerRadius="10" Padding="16" MinWidth="440">
  <StackPanel Spacing="12">

    <Grid ColumnDefinitions="*,Auto">
      <StackPanel VerticalAlignment="Center" Spacing="5">
        <TextBlock Text="Ollama" FontSize="16" FontWeight="Bold" Foreground="#E6EDF3"/>
        <Border Background="#1F6FEB26" CornerRadius="9" Padding="9,2" HorizontalAlignment="Left">
          <TextBlock Text="{Binding status}" FontSize="11" Foreground="#58A6FF"/>
        </Border>
      </StackPanel>
      <Button Grid.Column="1" Content="⟳  Refresh" Command="{Binding refresh}" VerticalAlignment="Top" Cursor="Hand"/>
    </Grid>

    <TextBlock Text="No models loaded in memory — nothing is holding VRAM."
               Foreground="#8B949E" FontSize="12" TextWrapping="Wrap"
               IsVisible="{Binding emptyVisible}"/>

    <ItemsControl ItemsSource="{Binding models}">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <Border Background="#161B22" BorderBrush="#21262D" BorderThickness="1"
                  CornerRadius="8" Padding="12,10" Margin="0,0,0,8">
            <Grid ColumnDefinitions="4,*,Auto">
              <Border Grid.Column="0" Width="4" CornerRadius="2" Background="#1F6FEB" Margin="0,2,12,2"/>
              <StackPanel Grid.Column="1" Spacing="3" VerticalAlignment="Center">
                <TextBlock Text="{Binding name}" FontWeight="SemiBold" FontSize="13" FontFamily="Consolas" Foreground="#E6EDF3"/>
                <TextBlock Text="{Binding detail}" FontSize="11" Foreground="#8B949E"/>
              </StackPanel>
              <Button Grid.Column="2" Content="Unload" Command="{Binding unload}" Cursor="Hand"
                      VerticalAlignment="Center"
                      Background="#2D1517" Foreground="#F85149" BorderBrush="#5A2A2E" BorderThickness="1"
                      CornerRadius="6" Padding="10,4"/>
            </Grid>
          </Border>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>

  </StackPanel>
</Border>
]]

local msg = dass.command.context.get_message()
if msg then
	msg:append_data(dass.ui.create_control(xaml, vm))
end

await refresh_fn()
