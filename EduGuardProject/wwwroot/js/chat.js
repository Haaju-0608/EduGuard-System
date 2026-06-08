"use strict";

var connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .withAutomaticReconnect()
    .build();

var sendButton = document.getElementById("sendButton");
var chatForm = document.getElementById("chatForm");
var messageInput = document.getElementById("messageInput");
var userInput = document.getElementById("userInput");
var messagesList = document.getElementById("messagesList");
var connectionStatus = document.getElementById("connectionStatus");

// Disable the send button until connection is established.
sendButton.disabled = true;

function setStatus(message, className) {
    connectionStatus.textContent = message;
    connectionStatus.className = className;
}

connection.on("ReceiveMessage", function (user, message) {
    var li = document.createElement("li");
    messagesList.appendChild(li);
    li.textContent = `${user} says ${message}`;
});

connection.onreconnecting(function () {
    sendButton.disabled = true;
    setStatus("Dang ket noi lai...", "status-connecting");
});

connection.onreconnected(function () {
    sendButton.disabled = false;
    setStatus("Da ket noi ChatHub", "status-connected");
});

connection.onclose(function () {
    sendButton.disabled = true;
    setStatus("Mat ket noi ChatHub. Hay refresh trang de thu lai.", "status-error");
});

connection.start().then(function () {
    sendButton.disabled = false;
    setStatus("Da ket noi ChatHub", "status-connected");
}).catch(function (err) {
    setStatus("Khong ket noi duoc ChatHub. Kiem tra console/server.", "status-error");
    return console.error(err.toString());
});

chatForm.addEventListener("submit", function (event) {
    event.preventDefault();

    var user = userInput.value.trim();
    var message = messageInput.value.trim();

    if (!user || !message) {
        return;
    }

    connection.invoke("SendMessage", user, message).catch(function (err) {
        return console.error(err.toString());
    });
    messageInput.value = "";
    messageInput.focus();
});
