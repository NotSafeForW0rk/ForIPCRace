﻿using Content.Server.Administration;
using Content.Server.Bible.Components;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Player;
using Content.Shared.Chat;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;

namespace Content.Server.Prayer;
/// <summary>
/// System to handle subtle messages and praying
/// </summary>
/// <remarks>
/// Rain is a professional developer and this did not take 2 PRs to fix subtle messages
/// </remarks>
public sealed class PrayerSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrayableComponent, GetVerbsEvent<ActivationVerb>>(AddPrayVerb);
    }

    private void AddPrayVerb(EntityUid uid, PrayableComponent comp, GetVerbsEvent<ActivationVerb> args)
    {
        // if it doesn't have an actor and we can't reach it then don't add the verb
        if (!EntityManager.TryGetComponent<ActorComponent?>(args.User, out var actor))
            return;

        // this is to prevent ghosts from using it
        if (!args.CanAccess)
            return;

        var prayerVerb = new ActivationVerb
        {
            Text = Loc.GetString("prayer-verbs-pray"),
            IconTexture = "/Textures/Interface/pray.svg.png",
            Act = () =>
            {
                if (comp.BibleUserOnly && !EntityManager.TryGetComponent<BibleUserComponent>(args.User, out var bibleUser))
                {
                    _popupSystem.PopupEntity(Loc.GetString("prayer-popup-notify-locked"), uid, Filter.Empty().AddPlayer(actor.PlayerSession), PopupType.Large);
                    return;
                }

                _quickDialog.OpenDialog(actor.PlayerSession, "Pray", "Message", (string message) =>
                {
                    Pray(actor.PlayerSession, message);
                });
            },
            Impact = LogImpact.Low,

        };
        prayerVerb.Impact = LogImpact.Low;
        args.Verbs.Add(prayerVerb);
    }

    /// <summary>
    /// Subtly messages a player by giving them a popup and a chat message.
    /// </summary>
    /// <param name="target">The IPlayerSession that you want to send the message to</param>
    /// <param name="messageString">The main message sent to the player via the chatbox</param>
    /// <param name="popupMessage">The popup to notify the player, also prepended to the messageString</param>
    public void SendSubtleMessage(IPlayerSession target, string messageString, string popupMessage)
    {
        if (target.AttachedEntity == null)
            return;

        var message = popupMessage == "" ? "" : popupMessage + $" \"{messageString}\"";

        _popupSystem.PopupEntity(popupMessage, target.AttachedEntity.Value, Filter.Empty().AddPlayer(target), PopupType.Large);
        _chatManager.ChatMessageToOne(ChatChannel.Local, messageString, message, EntityUid.Invalid, false, target.ConnectedClient);
    }

    /// <summary>
    /// Sends a message to the admin channel with a message and username
    /// </summary>
    /// <param name="sender">The IPlayerSession who sent the original message</param>
    /// <param name="message">Message to be sent to the admin chat</param>
    /// <remarks>
    /// You may be wondering, "Why the admin chat, specifically? Nobody even reads it!"
    /// Exactly.
    ///  </remarks>
    public void Pray(IPlayerSession sender, string message)
    {
        if (sender.AttachedEntity == null)
            return;


        _popupSystem.PopupEntity(Loc.GetString("prayer-popup-notify-sent"), sender.AttachedEntity.Value, Filter.Empty().AddPlayer(sender), PopupType.Medium);

        _chatManager.SendAdminAnnouncement(Loc.GetString("prayer-chat-notify", ("name", sender.Name), ("message", message)));
    }
}
