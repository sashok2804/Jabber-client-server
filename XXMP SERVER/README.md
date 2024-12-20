# XMPP Server

## Описание

**XMPP Server** — это серверная часть, реализующая протокол обмена сообщениями **XMPP (Jabber)**. Он предназначен для поддержки обмена текстовыми сообщениями, обновления статуса присутствия пользователей и хранения истории чатов в базе данных. Этот сервер был разработан в рамках курсовой работы на тему "Jabber-ICQ клиент с реализацией Jabber-протокола".

**Цель проекта:** создание серверной платформы для обмена сообщениями в реальном времени, соответствующей стандартам и принципам протокола XMPP. Проект может быть использован как основа для создания полноценного чат-приложения или для интеграции в другие системы, использующие XMPP.

## Особенности

- **Аутентификация**: поддержка регистрации и входа в систему с помощью логина и пароля.
- **Обмен сообщениями**: сервер поддерживает отправку и получение сообщений между пользователями в реальном времени.
- **Обновление присутствия**: пользователи могут обновлять свой статус (например, "в сети", "не в сети").
- **История чатов**: сервер позволяет пользователям загружать историю сообщений между собой.
- **Поддержка SQLite**: все данные (пользователи, сообщения, статусы) сохраняются в локальной базе данных SQLite.

## Почему XMPP?

**XMPP (Extensible Messaging and Presence Protocol)** — это открытый и расширяемый протокол для обмена сообщениями и присутствия. Он используется в различных приложениях и системах для организации чат-коммуникаций. В данном проекте XMPP был выбран как основной протокол по нескольким причинам:

- **Широкая поддержка**: XMPP является стандартом в мире обмена сообщениями и поддерживается многими приложениями и сервисами.
- **Гибкость и расширяемость**: благодаря возможности добавления новых расширений, XMPP идеально подходит для реализации расширенной функциональности.
- **Открытость**: протокол является открытым, что позволяет без ограничений использовать его для образовательных и исследовательских целей.

## Структура проекта

- **XMPPServer.cs** — главный класс сервера, управляющий подключениями, обработкой запросов и обменом сообщениями между клиентами.
- **XMPPHandlers.cs** — обработчики XMPP-команд: отправка сообщений, обновление статуса, аутентификация, регистрация и работа с историей чатов.
- **DatabaseHelper.cs** — взаимодействие с базой данных SQLite для хранения информации о пользователях и сообщениях.
- **LogHelper.cs** — конфигурация и настройка логирования с использованием Serilog для отслеживания операций и ошибок.

## 🚀 Запуск сервера

### Шаг 1: Клонирование репозитория

```bash
git clone https://github.com/yourusername/xmpp-server.git
cd xmpp-server
```

### Шаг 2: Установка зависимостей

Убедитесь, что у вас установлен .NET 6 или 7:

```bash
dotnet --version
```

Если .NET не установлен, скачайте его с официального сайта.

### Шаг 3: Запуск сервера

Для запуска сервера выполните команду:

```bash
dotnet run
```

После этого сервер будет слушать на порту **5222** и готов принимать подключения от клиентов.

## Как это работает

- **Авторизация и регистрация**: сервер принимает запросы на аутентификацию и регистрацию пользователей. Если данные верны, клиент получает доступ к функционалу сервера.
- **Отправка сообщений**: сервер обрабатывает входящие сообщения и отправляет их получателям в реальном времени.
- **Обновление присутствия**: пользователи могут обновлять свой статус (например, "в сети", "не в сети").
- **История чатов**: при запросе сервер отправляет историю чатов между пользователями.

## Важные моменты

- **Логирование**: проект использует **Serilog** для ведения логов всех операций. Это позволяет отслеживать действия сервера, ошибок и состояния.
- **Безопасность**: для обеспечения безопасности пользователей, пароли хранятся в виде хешей, что исключает хранение их в открытом виде.

## Возможности для улучшений

- **Поддержка TLS**: добавление возможности защищенной связи с использованием **SSL/TLS**.
- **Миграции базы данных**: использование **Entity Framework** для более удобного управления схемой базы данных и миграциями.
- **Масштабируемость**: улучшение производительности и масштабируемости для обработки большого количества пользователей и сообщений.

## 🛡 Лицензия

Проект распространяется по лицензии **MIT**.
