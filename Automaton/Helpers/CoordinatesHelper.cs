using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Numerics;

namespace Automaton.Helpers;

public static class CoordinatesHelper
{
    public static Lumina.Excel.ExcelSheet<Aetheryte> Aetherytes = Svc.Data.GetExcelSheet<Aetheryte>(Svc.ClientState.ClientLanguage);
    public static Lumina.Excel.ExcelSheet<MapMarker> AetherytesMap = Svc.Data.GetExcelSheet<MapMarker>(Svc.ClientState.ClientLanguage);

    public class MapLinkMessage(ushort chatType, string sender, string text, float x, float y, float scale, uint territoryId, string placeName, DateTime recordTime)
    {
        public static MapLinkMessage Empty => new(0, string.Empty, string.Empty, 0, 0, 100, 0, string.Empty, DateTime.Now);

        public ushort ChatType = chatType;
        public string Sender = sender;
        public string Text = text;
        public float X = x;
        public float Y = y;
        public float Scale = scale;
        public uint TerritoryId = territoryId;
        public string PlaceName = placeName;
        public DateTime RecordTime = recordTime;
    }

    public static string GetNearestAetheryte(MapLinkMessage maplinkMessage)
    {
        var aetheryteName = "";
        double distance = 0;
        foreach (var data in Aetherytes)
        {
            if (!data.IsAetheryte) continue;
            if (data.Territory.Value == null) continue;
            if (data.PlaceName.Value == null) continue;
            var scale = maplinkMessage.Scale;
            if (data.Territory.Value.RowId == maplinkMessage.TerritoryId)
            {
                var mapMarker = AetherytesMap.FirstOrDefault(m => m.DataType == 3 && m.DataKey == data.RowId);
                if (mapMarker == null)
                {
                    Svc.Log.Error($"Cannot find aetherytes position for {maplinkMessage.PlaceName}#{data.PlaceName.Value.Name}");
                    continue;
                }
                var AethersX = ConvertMapMarkerToMapCoordinate(mapMarker.X, scale);
                var AethersY = ConvertMapMarkerToMapCoordinate(mapMarker.Y, scale);
                var temp_distance = Math.Pow(AethersX - maplinkMessage.X, 2) + Math.Pow(AethersY - maplinkMessage.Y, 2);
                if (aetheryteName == "" || temp_distance < distance)
                {
                    distance = temp_distance;
                    aetheryteName = data.PlaceName.Value.Name;
                }
            }
        }
        return aetheryteName;
    }

    public static unsafe string GetNearestAetheryte(Vector3 pos, TerritoryType map)
    {
        var MapLink = new MapLinkPayload(map.RowId, map.Map.Row, (int)pos.X * 1000, (int)pos.Z * 1000);
        var fauxMapLinkMessage = new MapLinkMessage(
            0,
            "",
            "",
            MapLink.XCoord,
            MapLink.YCoord,
            100,
            map.RowId,
            "",
            DateTime.Now
        );
        return GetNearestAetheryte(fauxMapLinkMessage);
    }

    public static uint GetNearestAetheryte(int zoneID, Vector3 pos)
    {
        var aetheryte = 0u;
        double distance = 0;
        foreach (var data in Aetherytes)
        {
            if (!data.IsAetheryte) continue;
            if (data.Territory.Value == null) continue;
            if (data.PlaceName.Value == null) continue;
            if (data.Territory.Value.RowId == zoneID)
            {
                var mapMarker = AetherytesMap.FirstOrDefault(m => m.DataType == 3 && m.DataKey == data.RowId);
                if (mapMarker == null)
                {
                    Svc.Log.Error($"Cannot find aetherytes position for {zoneID}#{data.PlaceName.Value.Name}");
                    continue;
                }
                var AethersX = ConvertMapMarkerToMapCoordinate(mapMarker.X, 100);
                var AethersY = ConvertMapMarkerToMapCoordinate(mapMarker.Y, 100);
                var temp_distance = Math.Pow(AethersX - pos.X, 2) + Math.Pow(AethersY - pos.Z, 2);
                if (aetheryte == default || temp_distance < distance)
                {
                    distance = temp_distance;
                    aetheryte = data.RowId;
                }
            }
        }

        return aetheryte;
    }

    public static uint GetZoneMainAetheryte(uint zoneID) => Aetherytes.FirstOrDefault(a => a.Territory.Value != null && a.Territory.Value.RowId == zoneID).RowId;

    private static float ConvertMapMarkerToMapCoordinate(int pos, float scale)
    {
        var num = scale / 100f;
        var rawPosition = (int)((float)(pos - 1024.0) / num * 1000f);
        return ConvertRawPositionToMapCoordinate(rawPosition, scale);
    }

    private static float ConvertRawPositionToMapCoordinate(int pos, float scale)
    {
        var num = scale / 100f;
        return (float)((((pos / 1000f * num) + 1024.0) / 2048.0 * 41.0 / num) + 1.0);
    }

    public static void TeleportToAetheryte(MapLinkMessage maplinkMessage)
    {
        var aetheryteName = GetNearestAetheryte(maplinkMessage);
        if (aetheryteName != "")
        {
            Svc.Log.Info($"Teleporting to {aetheryteName}");
            Svc.Commands.ProcessCommand($"/tp {aetheryteName}");
        }
        else
        {
            Svc.Log.Error($"Cannot find nearest aetheryte of {maplinkMessage.PlaceName}({maplinkMessage.X}, {maplinkMessage.Y}).");
        }
    }

    private static TextPayload? GetInstanceIcon(int? instance)
    {
        return instance switch
        {
            1 => new TextPayload(SeIconChar.Instance1.ToIconString()),
            2 => new TextPayload(SeIconChar.Instance2.ToIconString()),
            3 => new TextPayload(SeIconChar.Instance3.ToIconString()),
            _ => default,
        };
    }

    private static uint FlagIconId = 60561U;

    private static int MapCordToInternal(double coord, double scale)
        => (int)(coord - 100 - (2048 / scale)) / 2;

    public static unsafe void Place(TerritoryType territory, float xCord, float yCord) => PlaceFromInternalCoords(territory.RowId, territory.Map.Row, xCord, yCord);
    public static unsafe void Place(ushort territory, float xCord, float yCord) => PlaceFromInternalCoords(Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territory).RowId, Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territory).Map.Row, xCord, yCord);

    public static unsafe void PlaceFromMapCoords(TerritoryType territory, float xCord, float yCord)
    {
        var sizeFactor = (territory.Map.Value?.SizeFactor ?? 100f) / 100f;
        var x = MapCordToInternal(xCord * 100, sizeFactor);
        var y = MapCordToInternal(yCord * 100, sizeFactor);
        PlaceFromInternalCoords(territory.RowId, territory.Map.Row, x, y);
    }

    private static unsafe void PlaceFromInternalCoords(uint territoryId, uint mapId, float xCord, float yCord)
    {
        var territory = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territoryId);
        var sizeFactor = (territory.Map.Value?.SizeFactor ?? 100f) / 100f;
        var x = MapCordToInternal(xCord, sizeFactor);
        var y = MapCordToInternal(yCord, sizeFactor);

        Svc.Log.Debug($"TerritoryId: {territoryId} at ({xCord},{yCord}) coords, sizeFactor: {sizeFactor}, adjusted coords ({x},{y})");
        var agentMap = AgentMap.Instance();
        agentMap->IsFlagMarkerSet = 0;

        agentMap->SetFlagMapMarker(territoryId, mapId, xCord, yCord, FlagIconId);
        agentMap->OpenMap(mapId, territoryId);
    }
}
