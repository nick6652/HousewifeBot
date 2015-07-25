﻿using System;
using System.Linq;
using System.Threading;
using DAL;
using Telegram;
using User = DAL.User;

namespace HousewifeBot
{
    public class SubscribeCommand : Command
    {
        public override bool Execute()
        {
            string serialTitle;
            if (string.IsNullOrEmpty(Arguments))
            {
                Program.Logger.Debug($"{GetType().Name}: Sending 'Enter serial title' prompt");
                try
                {
                    TelegramApi.SendMessage(Message.From, "Введите название сериала");
                }
                catch (Exception e)
                {
                    throw new Exception($"{GetType().Name}: An error occurred while sending prompt", e);
                }

                Program.Logger.Debug($"{GetType().Name}: Waiting for a message that contains serial title");
                try
                {
                    serialTitle = TelegramApi.WaitForMessage(Message.From).Text;
                }
                catch (Exception e)
                {
                    throw new Exception($"{GetType().Name}: An error occurred while waiting for a message that contains serial title", e);
                }
            }
            else
            {
                serialTitle = Arguments;
            }

            Program.Logger.Info($"{GetType().Name}: {Message.From.FirstName} {Message.From.FirstName} is trying to subscribe to '{serialTitle}'");

            string response;
            using (AppDbContext db = new AppDbContext())
            {
                Show show;
                Program.Logger.Debug($"{GetType().Name}: Searching serial {serialTitle} in database");
                try
                {
                    show = db.Shows.FirstOrDefault(s => s.Title.ToLower() == serialTitle.ToLower() ||
                                                        s.OriginalTitle.ToLower() == serialTitle.ToLower());
                }
                catch (Exception e)
                {
                    throw new Exception($"{GetType().Name}: An error occurred while searching serial {serialTitle} in database", e);
                }

                if (show != null)
                {
                    Program.Logger.Debug($"{GetType().Name}: Searching user with TelegramId: {Message.From.Id} in database");
                    User user;
                    try
                    {
                        user = db.Users.FirstOrDefault(u => u.TelegramUserId == Message.From.Id);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"{GetType().Name}: An error occurred while searching user in database", e);
                    }
                    bool newUser = false;
                    if (user == null)
                    {
                        user = new User
                        {
                            TelegramUserId = Message.From.Id,
                            FirstName = Message.From.FirstName,
                            LastName = Message.From.LastName,
                            Username = Message.From.Username
                        };
                        newUser = true;
                    }

                    if (newUser)
                    {
                        Program.Logger.Info($"{GetType().Name}: {user.FirstName} {user.LastName} is new User");
                    }
                    else
                    {
                        Program.Logger.Debug($"{GetType().Name}: User {user.FirstName} {user.LastName} is already exist");
                    }

                    bool subscriptionExists;
                    Program.Logger.Debug($"{GetType().Name}: Checking for subscription existence");
                    try
                    {
                        subscriptionExists = user.Subscriptions.Any(s => s.Show.Id == show.Id);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"{GetType().Name}: An error occurred while checking for subscription existence", e);
                    }
                    if (subscriptionExists)
                    {
                        Program.Logger.Info($"{GetType().Name}: User {Message.From.FirstName} {Message.From.LastName} is already subscribed to {show.OriginalTitle}");
                        response = $"Вы уже подписаны на сериал '{show.Title}'";
                    }
                    else
                    {
                        Subscription subscription = new Subscription
                        {
                            User = user,
                            Show = show,
                            SubscriptionDate = DateTimeOffset.Now
                        };

                        if (newUser)
                        {
                            user.Subscriptions.Add(subscription);
                            db.Users.Add(user);
                        }
                        else
                        {
                            db.Subscriptions.Add(subscription);
                        }
                        response = $"Вы подписались на сериал '{show.Title}'";
                    }

                    Program.Logger.Debug($"{GetType().Name}: Saving changes to database");
                    try
                    {
                        db.SaveChanges();
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"{GetType().Name}: An error occurred while saving changes to database", e);
                    }
                }
                else
                {
                    Program.Logger.Info($"{GetType().Name}: Serial {serialTitle} was not found");
                    response = $"Сериал '{serialTitle}' не найден";
                }
            }

            Program.Logger.Debug($"{GetType().Name}: Sending response to {Message.From.FirstName} {Message.From.LastName}");
            try
            {
                TelegramApi.SendMessage(Message.From, response);
            }
            catch (Exception e)
            {
                throw new Exception($"{GetType().Name}: An error occurred while sending response to {Message.From.FirstName} {Message.From.LastName}", e);
            }

            Status = true;
            return true;
        }
    }
}
