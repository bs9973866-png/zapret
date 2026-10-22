namespace ZFlow;

internal static class DomainCatalog
{
    public static readonly IReadOnlyDictionary<string, string[]> Categories =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ИИ и кодинг"] = Domains("""
                chatgpt.com openai.com auth.openai.com platform.openai.com api.openai.com oaistatic.com oaiusercontent.com
                claude.ai anthropic.com claudeusercontent.com perplexity.ai pplx.ai deepseek.com chat.deepseek.com
                gemini.google.com aistudio.google.com generativelanguage.googleapis.com ai.google.dev google.dev
                copilot.microsoft.com copilot.com githubcopilot.com github.com githubusercontent.com githubassets.com
                cursor.com windsurf.com codeium.com tabnine.com huggingface.co hf.co replicate.com openrouter.ai
                groq.com together.ai fireworks.ai mistral.ai chat.mistral.ai cohere.com poe.com character.ai
                blackbox.ai phind.com you.com lmarena.ai arena.ai fal.ai fal.run runpod.io
                """),
            ["Telegram"] = Domains("""
                telegram.org t.me telegram.me telegram.dog telegra.ph telesco.pe telegram-cdn.org cdn-telegram.org
                """),
            ["WhatsApp и Meta"] = Domains("""
                whatsapp.com whatsapp.net wa.me web.whatsapp.com whatsappbrand.com facebook.com fb.com fbcdn.net
                facebook.net fbsbx.com messenger.com m.me instagram.com cdninstagram.com ig.me threads.net meta.ai
                """),
            ["YouTube и Google"] = Domains("""
                youtube.com youtu.be youtube-nocookie.com youtubeeducation.com youtubekids.com youtubei.googleapis.com
                youtube.googleapis.com ytimg.com googlevideo.com ggpht.com googleusercontent.com google.com googleapis.com
                gstatic.com google-analytics.com googletagmanager.com accounts.google.com google.dev
                """),
            ["Discord"] = Domains("""
                discord.com discord.gg discordapp.com discordapp.net discord.media discordcdn.com discordstatus.com
                discord.new discord.gift discordactivities.com discordsays.com
                """),
            ["Соцсети"] = Domains("""
                x.com twitter.com t.co twimg.com pbs.twimg.com video.twimg.com tiktok.com tiktokv.com tiktokcdn.com
                tiktokcdn-us.com tiktokcdn-eu.com musical.ly byteoversea.com ibytedtos.com ibyteimg.com snssdk.com
                reddit.com redd.it redditmedia.com redditstatic.com signal.org snapchat.com sc-cdn.net pinterest.com
                pinimg.com linkedin.com licdn.com tumblr.com tumblr.net bsky.app blueskyweb.xyz vk.com vk.ru userapi.com
                ok.ru odnoklassniki.ru weibo.com wechat.com qq.com qpic.cn line.me
                """),
            ["Музыка и стриминг"] = Domains("""
                spotify.com scdn.co spotifycdn.com ps-cdn.co deezer.com dzcdn.net music.youtube.com music.apple.com
                soundcloud.com sndcdn.com bandcamp.com mixcloud.com tidal.com netflix.com netflix.net nflxvideo.net
                nflximg.net nflxso.net disneyplus.com disney.com dssott.com primevideo.com amazonvideo.com hulu.com
                max.com hbomax.com discoveryplus.com peacocktv.com paramountplus.com pluto.tv tubitv.com plex.tv
                crunchyroll.com bilibili.com biliapi.net iqiyi.com youku.com viki.com dailymotion.com dmcdn.net
                vimeo.com vimeocdn.com twitch.tv ttvnw.net jtvnw.net kick.com rumble.com
                """),
            ["Игры и лаунчеры"] = Domains("""
                steampowered.com steamcommunity.com steamstatic.com steamcontent.com steam-chat.com steamserver.net
                steamusercontent.com steamdeck.com epicgames.com epicgames.dev unrealengine.com fortnite.com ea.com
                origin.com battle.net blizzard.com blizzard.net riotgames.com leagueoflegends.com valorant.com riotcdn.net
                ubisoft.com uplay.com rockstargames.com socialclub.rockstargames.com gog.com itch.io itch.zone xbox.com
                xboxlive.com playstation.com playstation.net nintendo.com nintendo.net wargaming.net
                minecraft.net mojang.com minecraftservices.com curseforge.com modrinth.com fabricmc.net papermc.io
                spigotmc.org planetminecraft.com namemc.com optifine.net roblox.com rbxcdn.com
                """),
            ["Разработка и облака"] = Domains("""
                gitlab.com gitlab-static.net bitbucket.org atlassian.com stackoverflow.com stackexchange.com npmjs.com
                npmjs.org nodejs.org pypi.org pythonhosted.org rubygems.org crates.io rust-lang.org maven.org gradle.org
                docker.com docker.io hub.docker.com dev.azure.com visualstudio.com vscode.dev code.visualstudio.com
                jetbrains.com replit.com codesandbox.io vercel.com netlify.app render.com fly.io railway.app
                cloudflare.com workers.dev pages.dev cloudfront.net amazonaws.com azure.com azureedge.net
                """),
            ["Мессенджеры и встречи"] = Domains("""
                signal.org whatsapp.com telegram.org discord.com skype.com teams.microsoft.com teams.live.com zoom.us
                zoom.com webex.com slack.com slack-edge.com element.io matrix.org meet.google.com
                """),
            ["Новости и поиск"] = Domains("""
                duckduckgo.com startpage.com brave.com search.brave.com ecosia.org yandex.com yandex.ru yastatic.net
                ya.ru rambler.ru proton.me protonmail.com fastmail.com theguardian.com nytimes.com washingtonpost.com
                bbc.com bbc.co.uk reuters.com apnews.com bloomberg.com economist.com wired.com arstechnica.com techcrunch.com
                """),
            ["Хранилища и продуктивность"] = Domains("""
                dropbox.com dropboxapi.com dropboxusercontent.com box.com mega.io mega.nz mediafire.com wetransfer.com
                pcloud.com sync.com icloud.com icloud-content.com apple-cloudkit.com drive.google.com storage.googleapis.com
                blob.core.windows.net notion.so notion.site figma.com figmausercontent.com canva.com miro.com linear.app
                asana.com trello.com grammarly.com medium.com substack.com evernote.com todoist.com calendly.com airtable.com
                """),
            ["Apple и Microsoft"] = Domains("""
                apple.com icloud.com mzstatic.com itunes.apple.com apps.apple.com appstore.com apple.news cdn-apple.com
                microsoft.com microsoftonline.com live.com bing.com bing.net office.com office.net outlook.com windows.net
                msauth.net msftauth.net msidentity.com sharepoint.com onedrive.live.com 1drv.com aka.ms
                """),
        };

    public static readonly HashSet<string> DefaultCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "ИИ и кодинг", "Telegram", "WhatsApp и Meta", "YouTube и Google", "Discord", "Соцсети", "Музыка и стриминг"
    };

    public static int TotalDomains => Categories.Values.SelectMany(x => x).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    public static string[] GetDomains(IEnumerable<string> categories, string custom)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string category in categories)
            if (Categories.TryGetValue(category, out string[]? domains))
                result.UnionWith(domains);

        foreach (string entry in custom.Split(new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string host = entry.Trim().TrimStart('.');
            if (IsValidHost(host))
                result.Add(host);
        }
        return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsValidHost(string host)
    {
        if (host.Length is < 3 or > 253 || host.Contains("://", StringComparison.Ordinal))
            return false;
        string[] labels = host.Split('.');
        return labels.Length >= 2 && labels.All(label =>
            label.Length is >= 1 and <= 63 &&
            char.IsAsciiLetterOrDigit(label[0]) &&
            char.IsAsciiLetterOrDigit(label[^1]) &&
            label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
    }

    private static string[] Domains(string raw) =>
        raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Distinct(StringComparer.OrdinalIgnoreCase)
           .ToArray();
}
