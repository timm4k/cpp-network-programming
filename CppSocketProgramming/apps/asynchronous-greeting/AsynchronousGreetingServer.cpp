#include "ApplicationSupport.h"

#include <exception>
#include <iostream>

int main(int argc, char* argv[])
{
    try
    {
        application::configure_console();
        const network::WinsockRuntime winsock;
        const auto port = network::parse_server_port(argc, argv, application::GreetingPort);
        const network::Socket listener = network::create_listener(port);
        const network::NetworkEvent acceptEvent;
        std::cout << "Asynchronous greeting server is listening on 127.0.0.1:" << port << '\n';

        const network::Socket client = network::accept_async(listener, acceptEvent);
        const network::NetworkEvent clientEvent;
        application::print_received(network::remote_endpoint(client), network::receive_line_async(client, clientEvent));
        network::send_line_async(client, clientEvent, application::GreetingResponse);
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}

