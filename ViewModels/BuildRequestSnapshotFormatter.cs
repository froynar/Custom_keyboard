using System.Globalization;
using System.Text;
using System.Text.Json;
using Custom_keyboard.Localization;

namespace Custom_keyboard.ViewModels;

internal static class BuildRequestSnapshotFormatter
{
    public static string Format(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return Loc.Instance["Snapshot_Empty"];
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return payloadJson;
            }

            var text = new StringBuilder();
            AppendBuild(text, root);
            AppendKit(text, root);
            AppendItems(text, root);
            AppendMods(text, root);
            AppendSeller(text, root);

            return text.ToString().TrimEnd();
        }
        catch (JsonException)
        {
            return payloadJson;
        }
    }

    private static void AppendBuild(StringBuilder text, JsonElement root)
    {
        if (!TryGetObject(root, "build", out var build))
        {
            return;
        }

        AppendLine(text, "Build", JoinNonEmpty(
            GetString(build, "name"),
            FormatParen(GetString(build, "buildId"))));
        AppendLine(text, Loc.Instance["Snapshot_Status"], GetString(build, "status"));
        AppendLine(text, Loc.Instance["Snapshot_NoiseRequirement"], GetString(build, "noiseRequirement"));
        AppendLine(text, Loc.Instance["Snapshot_Total"], FormatMoney(GetDecimal(build, "totalCostSnapshot")));
        AppendLine(text, Loc.Instance["Snapshot_Notes"], GetString(build, "notes"));
        text.AppendLine();
    }

    private static void AppendKit(StringBuilder text, JsonElement root)
    {
        if (!TryGetObject(root, "kit", out var kit))
        {
            AppendLine(text, "Kit", Loc.Instance["Snapshot_NoKit"]);
            text.AppendLine();
            return;
        }

        AppendLine(text, "Kit", JoinNonEmpty(
            GetString(kit, "kitName"),
            FormatParen(GetString(kit, "kitId"))));
        AppendLine(text, "PCB", GetString(kit, "pcbTechnology"));
        AppendLine(text, "Switch mount", GetString(kit, "switchMount"));
        AppendLine(text, Loc.Instance["Snapshot_SwitchNeeded"], GetInt(kit, "requiredSwitchQuantity")?.ToString(CultureInfo.InvariantCulture));
        AppendLine(text, Loc.Instance["Snapshot_KitPrice"], FormatMoney(GetDecimal(kit, "priceUsd")));
        AppendLine(text, Loc.Instance["Snapshot_Included"], GetString(kit, "includedParts"));
        text.AppendLine();
    }

    private static void AppendItems(StringBuilder text, JsonElement root)
    {
        text.AppendLine(Loc.Instance["Snapshot_Items"]);

        if (!TryGetArray(root, "items", out var items) || items.GetArrayLength() == 0)
        {
            text.AppendLine("- " + Loc.Instance["Snapshot_NoItems"]);
            text.AppendLine();
            return;
        }

        foreach (var item in items.EnumerateArray())
        {
            var type = GetString(item, "productType") ?? "Item";
            var name = GetString(item, "productName") ?? GetString(item, "productId") ?? "Unknown";
            var productId = FormatParen(GetString(item, "productId"));
            var quantity = GetInt(item, "quantity") ?? 0;
            var unitPrice = FormatMoney(GetDecimal(item, "unitPriceSnapshot"));
            var lineTotal = FormatMoney(GetDecimal(item, "lineTotal"));

            text.Append("- ")
                .Append(type)
                .Append(": ")
                .Append(JoinNonEmpty(name, productId));

            if (quantity > 0)
            {
                text.Append(" x").Append(quantity.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(unitPrice) || !string.IsNullOrWhiteSpace(lineTotal))
            {
                text.Append(" - ");
                if (!string.IsNullOrWhiteSpace(unitPrice))
                {
                    text.Append(unitPrice);
                }

                if (!string.IsNullOrWhiteSpace(lineTotal))
                {
                    text.Append(" = ").Append(lineTotal);
                }
            }

            text.AppendLine();
            AppendIndented(text, Loc.Instance["Snapshot_Note"], GetString(item, "notes"));
        }

        text.AppendLine();
    }

    private static void AppendMods(StringBuilder text, JsonElement root)
    {
        if (!TryGetArray(root, "mods", out var mods) || mods.GetArrayLength() == 0)
        {
            return;
        }

        text.AppendLine(Loc.Instance["Snapshot_Mods"]);
        foreach (var mod in mods.EnumerateArray())
        {
            var label = JoinNonEmpty(GetString(mod, "modType"), GetString(mod, "targetComponent"));
            text.Append("- ").Append(string.IsNullOrWhiteSpace(label) ? "Mod" : label).AppendLine();
            AppendIndented(text, Loc.Instance["Snapshot_Note"], GetString(mod, "notes"));
        }

        text.AppendLine();
    }

    private static void AppendSeller(StringBuilder text, JsonElement root)
    {
        if (!TryGetObject(root, "seller", out var seller))
        {
            return;
        }

        AppendLine(text, "Seller", JoinNonEmpty(
            GetString(seller, "shopName"),
            FormatParen(GetInt(seller, "sellerUserId")?.ToString(CultureInfo.InvariantCulture))));
        AppendLine(text, Loc.Instance["Snapshot_SellerPhone"], GetString(seller, "phone"));
        AppendLine(text, Loc.Instance["Snapshot_SellerAddress"], GetString(seller, "address"));
    }

    private static void AppendLine(StringBuilder text, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        text.Append(label).Append(": ").AppendLine(value.Trim());
    }

    private static void AppendIndented(StringBuilder text, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        text.Append("  ").Append(label).Append(": ").AppendLine(value.Trim());
    }

    private static bool TryGetObject(JsonElement parent, string propertyName, out JsonElement value)
        => TryGetProperty(parent, propertyName, out value) && value.ValueKind == JsonValueKind.Object;

    private static bool TryGetArray(JsonElement parent, string propertyName, out JsonElement value)
        => TryGetProperty(parent, propertyName, out value) && value.ValueKind == JsonValueKind.Array;

    private static bool TryGetProperty(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (parent.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        var pascalName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        return parent.TryGetProperty(pascalName, out value);
    }

    private static string? GetString(JsonElement parent, string propertyName)
    {
        if (!TryGetProperty(parent, propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static int? GetInt(JsonElement parent, string propertyName)
    {
        if (!TryGetProperty(parent, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    private static decimal? GetDecimal(JsonElement parent, string propertyName)
    {
        if (!TryGetProperty(parent, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    private static string? FormatMoney(decimal? value)
        => value is null ? null : $"{value.Value:N2} USD";

    private static string? FormatParen(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : $"({value.Trim()})";

    private static string JoinNonEmpty(params string?[] values)
        => string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
}
