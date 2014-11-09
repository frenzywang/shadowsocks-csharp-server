shadowsocks
===============

A fast tunnel proxy that helps you bypass firewalls.

Install
-------

Install Runtime .Net 2.0

Download binary from http://blog.mengsky.net/test/shadowsocks.net.rar.

Edit config.json

    {
        "server":"my_server_ip",
        "server_port":8388,
        "password":"mypassword",
        "timeout":300,
        "method":"aes-128-cfb",
    }

Encrypt method
-----------------
 
    "table",
    "rc4",
    "aes-256-cfb",
    "aes-192-cfb",
    "aes-128-cfb",
    "bf-cfb"

License
-----------------
MIT

Bugs and Issues
----------------
