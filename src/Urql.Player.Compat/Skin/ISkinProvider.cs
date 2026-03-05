namespace Urql.Player.Compat.Skin;

public interface ISkinProvider
{
    SkinDefinition LoadLegacyXml(string path);
    SkinDefinition LoadJson(string path);
    SkinDefinition LoadBuiltInDefault();
}
