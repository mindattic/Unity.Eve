using System;
using System.Collections.Generic;
using Unity.Eve.Core.Session;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    /// <summary>
    /// The multiplayer-ready seam (U.EVE-LAW-6): two seats drive the session with their own commands,
    /// neither touches the other's controls, and a command from a seat that isn't there is refused.
    /// </summary>
    public class SessionTests
    {
        private static IntentCommand Intent(int seat, long frame, float x = 0f, float y = 0f, IntentButtons buttons = IntentButtons.None, int navY = 0) =>
            new IntentCommand(PlayerId.Of(seat), frame, x, y, 0, navY, buttons);

        [Test]
        public void Two_players_drive_their_own_controls_independently()
        {
            var session = new LocalSession(players: 2);
            var kat = session.Players[0];
            var aya = session.Players[1];

            Assert.That(session.Submit(Intent(kat.Seat, 1, x: -1f, buttons: IntentButtons.Confirm)), Is.True);
            Assert.That(session.Submit(Intent(aya.Seat, 1, y: 1f, buttons: IntentButtons.Cancel | IntentButtons.InteractHeld)), Is.True);

            var k = session.ControlsOf(kat);
            var a = session.ControlsOf(aya);
            Assert.That((k.MoveX, k.MoveY), Is.EqualTo((-1f, 0f)));
            Assert.That((a.MoveX, a.MoveY), Is.EqualTo((0f, 1f)));
            Assert.That(k.ConfirmPressed, Is.True);
            Assert.That(k.CancelPressed, Is.False);
            Assert.That(a.ConfirmPressed, Is.False);
            Assert.That(a.CancelPressed, Is.True);
            Assert.That(a.InteractHeld, Is.True);
            Assert.That(k.InteractHeld, Is.False);
        }

        [Test]
        public void A_command_from_a_seat_not_in_the_session_is_refused()
        {
            var session = new LocalSession(players: 2);
            var refused = new List<CommandRefused>();
            session.EventRaised += e => { if (e is CommandRefused r) refused.Add(r); };

            Assert.That(session.Submit(Intent(2, 1, x: 1f)), Is.False);
            Assert.That(session.Submit(new PressCommand(PlayerId.Of(-1), ScriptedButton.Dodge)), Is.False);
            Assert.That(refused.Count, Is.EqualTo(2));
            Assert.That(session.ControlsOf(PlayerId.Of(2)), Is.Null);
            Assert.That(session.ControlsOf(session.Players[0]).MoveX, Is.EqualTo(0f));
            Assert.That(session.ControlsOf(session.Players[1]).MoveX, Is.EqualTo(0f));
            Assert.That(session.Accepted, Is.EqualTo(0));
        }

        [Test]
        public void A_stale_intent_is_refused()
        {
            var session = new LocalSession();
            session.Submit(Intent(0, 10, x: 1f));
            CommandRefused refused = null;
            session.EventRaised += e => refused = e as CommandRefused;
            Assert.That(session.Submit(Intent(0, 9, x: -1f)), Is.False);
            Assert.That(refused, Is.Not.Null);
            Assert.That(session.ControlsOf(session.LocalPlayer).MoveX, Is.EqualTo(1f));
            Assert.That(session.Submit(Intent(0, 10, x: 0.5f)), Is.True, "the same frame may be resubmitted");
        }

        [Test]
        public void A_scripted_press_reaches_only_its_seat_and_only_the_first_reader()
        {
            var session = new LocalSession(players: 2);
            session.Submit(new PressCommand(PlayerId.Of(1), ScriptedButton.Dodge));
            var p1 = session.ControlsOf(PlayerId.Of(0));
            var p2 = session.ControlsOf(PlayerId.Of(1));
            Assert.That(p1.CancelPressed, Is.False);
            Assert.That(p2.CancelPressed, Is.True);
            Assert.That(p2.CancelPressed, Is.False, "a scripted press is consumed by its first reader");
        }

        [Test]
        public void A_controller_edge_is_seen_by_every_reader_in_its_frame_and_cleared_by_the_next()
        {
            var session = new LocalSession();
            var c = session.ControlsOf(session.LocalPlayer);
            session.Submit(Intent(0, 1, buttons: IntentButtons.Confirm));
            Assert.That(c.ConfirmPressed, Is.True);
            Assert.That(c.ConfirmPressed, Is.True);
            session.Submit(Intent(0, 2));
            Assert.That(c.ConfirmPressed, Is.False);
        }

        [Test]
        public void The_local_player_is_one_seat_and_defaults_to_the_first()
        {
            Assert.That(new LocalSession().LocalPlayer, Is.EqualTo(PlayerId.Of(0)));
            Assert.That(new LocalSession(players: 2, localSeat: 1).LocalPlayer, Is.EqualTo(PlayerId.Of(1)));
            Assert.That(new LocalSession(players: 2, localSeat: 5).LocalPlayer, Is.EqualTo(PlayerId.Of(0)));
            Assert.That(new LocalSession(players: 0).Players.Count, Is.EqualTo(1));
        }

        [Test]
        public void Commands_round_trip_through_the_codec()
        {
            var commands = new IGameCommand[]
            {
                new IntentCommand(PlayerId.Of(1), 12345678901L, 0.123456789f, -1f, -1, 1,
                    IntentButtons.Confirm | IntentButtons.ConfirmHeld | IntentButtons.FocusRiposte),
                new PressCommand(PlayerId.Of(0), ScriptedButton.Shootdodge),
            };
            foreach (var command in commands)
            {
                var line = CommandCodec.Encode(command);
                var back = CommandCodec.Decode(line);
                Assert.That(CommandCodec.Encode(back), Is.EqualTo(line));
                Assert.That(back.Player, Is.EqualTo(command.Player));
            }
            var intent = (IntentCommand)CommandCodec.Decode(CommandCodec.Encode(commands[0]));
            Assert.That(intent.MoveX, Is.EqualTo(0.123456789f));
            Assert.That(intent.Frame, Is.EqualTo(12345678901L));
        }

        [TestCase("")]
        [TestCase("intent|0|1")]
        [TestCase("press|x|Dodge")]
        [TestCase("teleport|0|1|2")]
        public void The_codec_rejects_lines_it_did_not_write(string line)
        {
            Assert.Throws<FormatException>(() => CommandCodec.Decode(line));
        }

        [Test]
        public void Replaying_a_command_log_reproduces_each_seats_controls()
        {
            var log = new[]
            {
                CommandCodec.Encode(Intent(0, 1, x: 0.5f)),
                CommandCodec.Encode(Intent(1, 1, y: -0.25f, navY: 1)),
                CommandCodec.Encode(Intent(0, 2, x: 0.75f, buttons: IntentButtons.Menu)),
            };
            LocalSession Replay()
            {
                var s = new LocalSession(players: 2);
                foreach (var line in log) s.Submit(CommandCodec.Decode(line));
                return s;
            }
            var a = Replay();
            var b = Replay();
            foreach (var p in a.Players)
            {
                Assert.That(b.ControlsOf(p).MoveX, Is.EqualTo(a.ControlsOf(p).MoveX));
                Assert.That(b.ControlsOf(p).MoveY, Is.EqualTo(a.ControlsOf(p).MoveY));
                Assert.That(b.ControlsOf(p).NavigateY, Is.EqualTo(a.ControlsOf(p).NavigateY));
                Assert.That(b.ControlsOf(p).MenuPressed, Is.EqualTo(a.ControlsOf(p).MenuPressed));
            }
            Assert.That(a.ControlsOf(PlayerId.Of(0)).MoveX, Is.EqualTo(0.75f));
            Assert.That(a.ControlsOf(PlayerId.Of(1)).NavigateY, Is.EqualTo(1));
        }
    }
}
